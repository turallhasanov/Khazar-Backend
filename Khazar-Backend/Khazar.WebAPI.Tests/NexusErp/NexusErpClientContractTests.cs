using System.Net;
using System.Text.Json.Nodes;
using Khazar.Domain.Entities;
using Khazar.Domain.ValueObjects;
using Khazar.Infrastructure.ExternalServices;
using Khazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Khazar.WebAPI.Tests.NexusErp;

/// <summary>
/// Khazar's side of the NexusERP contract: the requests NexusErpClient sends (verb, path, JSON body) and how it reads
/// NexusERP's answers, including its error bodies and the RFC 7807 500 NexusERP returns for unhandled exceptions.
/// </summary>
public sealed class NexusErpClientContractTests : IDisposable
{
    private const string Sku = "KHZ-COAT-BLK-M";

    private readonly KhazarDbContext _db = new(new DbContextOptionsBuilder<KhazarDbContext>()
        .UseInMemoryDatabase("nexus-client-" + Guid.NewGuid()).Options);
    private readonly StubNexusErp _nexus = new();

    public void Dispose() => _db.Dispose();

    private NexusErpClient Client(string storeId = "store-1", string? warehouseId = null) => new(
        new HttpClient(_nexus) { BaseAddress = new Uri("http://nexus.test/") },
        _db,
        Options.Create(new NexusErpOptions { BaseUrl = "http://nexus.test", StoreId = storeId, WarehouseId = warehouseId }),
        NullLogger<NexusErpClient>.Instance);

    private static string Stocks(params (string Warehouse, string ProductId, string Barcode, decimal Quantity)[] rows) =>
        new JsonObject
        {
            ["ok"] = true,
            ["items"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(), ["warehouseId"] = r.Warehouse,
                ["warehouse"] = new JsonObject { ["id"] = r.Warehouse, ["code"] = "ANB-001", ["name"] = "Anbar" },
                ["productId"] = r.ProductId,
                ["product"] = new JsonObject { ["id"] = r.ProductId, ["code"] = "MHS-001", ["name"] = "Palto", ["barcode"] = r.Barcode, ["unit"] = "əd." },
                ["quantity"] = r.Quantity
            }).ToArray())
        }.ToJsonString();

    private async Task<(Order Order, ProductVariant Variant)> OrderAsync(string size = "M", string color = "Qara", string sku = Sku)
    {
        var category = new Category("Coat", "Palto", "palto", 0);
        var product = new Product("Yun palto", "yun-palto-" + Guid.NewGuid().ToString("N")[..6], "", 149.90m, "Yun", category, "/images/coat.jpg");
        var variant = product.AddVariant(size, color, sku, 5);
        _db.AddRange(category, product);
        await _db.SaveChangesAsync();

        var order = new Order("Leyla Həsənova", "leyla@example.az", new Address("Nizami 10", "Bakı", "AZ1000", "Azərbaycan"));
        order.AddItem(variant, product.Name, 149.90m, 2);
        return (order, variant);
    }

    // ---- Stock (GET api/inventory/stocks) ----

    [Fact]
    public async Task Stock_is_the_whole_unit_sum_over_warehouses_for_the_sku()
    {
        _nexus.On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.OK,
            Stocks(("w-1", "p-1", Sku, 3.5m), ("w-2", "p-1", Sku, 2m), ("w-1", "p-2", "OTHER", 50m)));

        (await Client().GetStockBySkuAsync("khz-coat-blk-m")).Should().Be(5);
    }

    [Fact]
    public async Task Stock_is_limited_to_the_configured_warehouse()
    {
        _nexus.On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.OK, Stocks(("w-1", "p-1", Sku, 3m), ("w-2", "p-1", Sku, 2m)));

        (await Client(warehouseId: "w-2").GetStockBySkuAsync(Sku)).Should().Be(2);
    }

    [Fact]
    public async Task A_negative_nexus_balance_is_reported_as_zero()
    {
        _nexus.On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.OK, Stocks(("w-1", "p-1", Sku, -4m)));

        (await Client().GetStockBySkuAsync(Sku)).Should().Be(0);
    }

    [Fact]
    public async Task No_stock_row_is_zero_for_a_known_product_and_unknown_otherwise()
    {
        _nexus.On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.OK, Stocks())
            .On(HttpMethod.Get, "api/products/barcode/KNOWN", HttpStatusCode.OK, """{"ok":true,"product":{"id":"p-9","barcode":"KNOWN"}}""")
            .On(HttpMethod.Get, "api/products/barcode/UNKNOWN", HttpStatusCode.NotFound, """{"ok":false,"error":"Bu barkodla məhsul tapılmadı."}""");

        (await Client().GetStockBySkuAsync("known")).Should().Be(0);
        (await Client().GetStockBySkuAsync("unknown")).Should().BeNull();
    }

    [Fact]
    public async Task An_unreachable_or_failing_nexus_falls_back_to_null()
    {
        _nexus.On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.InternalServerError,
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.","status":500,"traceId":"00-1","ok":false,"error":"Gözlənilməz xəta baş verdi."}""");
        (await Client().GetStockBySkuAsync(Sku)).Should().BeNull();

        _nexus.Throw(HttpMethod.Get, "api/inventory/stocks", new HttpRequestException("Connection refused"));
        (await Client().GetStockBySkuAsync(Sku)).Should().BeNull();
    }

    [Fact]
    public async Task A_cancellation_the_caller_asked_for_is_not_swallowed()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Client().Invoking(c => c.GetStockBySkuAsync(Sku, cancelled.Token)).Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- Catalog pull (GET api/products + api/inventory/stocks) ----

    [Fact]
    public async Task The_catalog_pull_keeps_active_products_with_a_barcode_and_their_stock()
    {
        _nexus.On(HttpMethod.Get, "api/products", HttpStatusCode.OK, """
            {"ok":true,"items":[
              {"id":"p-1","code":"MHS-001","name":" Yun palto ","barcode":"khz-1","salePrice":149.9,"isActive":true,"category":{"id":"c","code":"KAT-001","name":" Palto "}},
              {"id":"p-2","code":"MHS-002","name":"Köhnə","barcode":"KHZ-2","salePrice":10,"isActive":false,"category":{"id":"c","code":"KAT-001","name":"Palto"}},
              {"id":"p-3","code":"MHS-003","name":"Barkodsuz","barcode":"  ","salePrice":10,"isActive":true,"category":null},
              {"id":"p-4","code":"MHS-004","name":"Şərf","barcode":"KHZ-4","salePrice":25,"isActive":true,"category":{"id":"c","code":"KAT-002","name":""}}
            ]}
            """)
            .On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.OK, Stocks(("w-1", "p-1", "KHZ-1", 2.9m), ("w-2", "p-1", "KHZ-1", 4m)));

        var products = await Client().GetProductsAsync();

        products.Should().BeEquivalentTo(new[]
        {
            new Khazar.Application.Common.Interfaces.NexusProductSnapshot("KHZ-1", "Yun palto", "Palto", 149.9m, 6),
            new Khazar.Application.Common.Interfaces.NexusProductSnapshot("KHZ-4", "Şərf", null, 25m, 0),
        });
    }

    [Fact]
    public async Task The_catalog_pull_is_skipped_when_either_list_fails()
    {
        _nexus.On(HttpMethod.Get, "api/products", HttpStatusCode.OK, """{"ok":true,"items":[]}""")
            .On(HttpMethod.Get, "api/inventory/stocks", HttpStatusCode.Forbidden, """{"ok":false,"error":"Şirkət seçilməyib"}""");

        (await Client().GetProductsAsync()).Should().BeNull();
    }

    // ---- Order push (POST api/online-orders) ----

    [Fact]
    public async Task An_order_is_sent_with_the_storefront_prices_and_the_nexus_product_id()
    {
        var (order, _) = await OrderAsync();
        _nexus.On(HttpMethod.Get, $"api/products/barcode/{Sku}", HttpStatusCode.OK, """{"ok":true,"product":{"id":"p-1","barcode":"KHZ-COAT-BLK-M"}}""")
            .On(HttpMethod.Post, "api/online-orders", HttpStatusCode.OK, """{"ok":true,"id":"s-1","documentNumber":"STS-001","status":"approved"}""");

        var result = await Client().PushOrderToErpAsync(order);

        result.Success.Should().BeTrue();
        var body = JsonNode.Parse(_nexus.To(HttpMethod.Post, "api/online-orders").Single().Body!)!;
        body["externalOrderNumber"]!.GetValue<string>().Should().Be(order.OrderNumber);
        body["storeId"]!.GetValue<string>().Should().Be("store-1");
        body["warehouseId"].Should().BeNull();
        body["customer"]!["fullName"]!.GetValue<string>().Should().Be("Leyla Həsənova");
        body["customer"]!["email"]!.GetValue<string>().Should().Be("leyla@example.az");
        body["customer"]!["address"]!.GetValue<string>().Should().Be("Nizami 10, Bakı AZ1000, Azərbaycan");
        var line = body["items"]!.AsArray().Single()!;
        line["productId"]!.GetValue<string>().Should().Be("p-1");
        line["quantity"]!.GetValue<decimal>().Should().Be(2);
        line["unitPrice"]!.GetValue<decimal>().Should().Be(149.90m);
    }

    [Theory]
    [InlineData("M", "Qara", "Yun palto (Qara / M)")]
    [InlineData("Standart", "Standart", "Yun palto")]
    [InlineData("STANDART", "Bej", "Yun palto (Bej)")]
    public async Task A_variant_nexus_does_not_know_is_created_with_the_shared_variant_name(string size, string color, string expectedName)
    {
        var (order, _) = await OrderAsync(size, color);
        _nexus.On(HttpMethod.Get, $"api/products/barcode/{Sku}", HttpStatusCode.NotFound, """{"ok":false,"error":"Bu barkodla məhsul tapılmadı."}""")
            .On(HttpMethod.Get, "api/categories", HttpStatusCode.OK, """{"ok":true,"items":[{"id":"c-other","code":"KAT-001","name":"Şərf"}]}""")
            .On(HttpMethod.Post, "api/categories", HttpStatusCode.OK, """{"ok":true,"id":"c-1","code":"KAT-002","name":"Coat"}""")
            .On(HttpMethod.Get, "api/units", HttpStatusCode.OK, """{"ok":true,"items":[{"id":"u-1","name":"ədəd","shortName":"əd."}]}""")
            .On(HttpMethod.Post, "api/products", HttpStatusCode.OK, """{"ok":true,"id":"p-new","code":"MHS-010","barcode":"KHZ-COAT-BLK-M"}""")
            .On(HttpMethod.Post, "api/online-orders", HttpStatusCode.OK, """{"ok":true,"id":"s-1","documentNumber":"STS-001"}""");

        var result = await Client().PushOrderToErpAsync(order);

        result.Success.Should().BeTrue(result.Error);
        JsonNode.Parse(_nexus.To(HttpMethod.Post, "api/categories").Single().Body!)!["name"]!.GetValue<string>().Should().Be("Coat");
        _nexus.To(HttpMethod.Post, "api/units").Should().BeEmpty("'Ədəd' matches the existing unit case-insensitively");

        var product = JsonNode.Parse(_nexus.To(HttpMethod.Post, "api/products").Single().Body!)!;
        product["name"]!.GetValue<string>().Should().Be(expectedName);
        product["barcode"]!.GetValue<string>().Should().Be(Sku);
        product["categoryId"]!.GetValue<string>().Should().Be("c-1");
        product["unitId"]!.GetValue<string>().Should().Be("u-1");
        product["salePrice"]!.GetValue<decimal>().Should().Be(149.90m);

        JsonNode.Parse(_nexus.To(HttpMethod.Post, "api/online-orders").Single().Body!)!["items"]![0]!["productId"]!
            .GetValue<string>().Should().Be("p-new");
    }

    [Fact]
    public async Task A_rejected_order_carries_nexus_error_text()
    {
        var (order, _) = await OrderAsync();
        _nexus.On(HttpMethod.Get, $"api/products/barcode/{Sku}", HttpStatusCode.OK, """{"ok":true,"product":{"id":"p-1"}}""")
            .On(HttpMethod.Post, "api/online-orders", HttpStatusCode.BadRequest, """{"ok":false,"error":"Seçilmiş mağaza aktiv deyil."}""");

        var result = await Client().PushOrderToErpAsync(order);

        result.Should().Be(new Khazar.Application.Common.Interfaces.ErpPushResult(false, "NexusERP sifarişi rədd etdi (HTTP 400): Seçilmiş mağaza aktiv deyil."));
    }

    [Fact]
    public async Task Nexus_RFC7807_500_is_read_through_its_error_field()
    {
        var (order, _) = await OrderAsync();
        _nexus.On(HttpMethod.Get, $"api/products/barcode/{Sku}", HttpStatusCode.OK, """{"ok":true,"product":{"id":"p-1"}}""")
            .On(HttpMethod.Post, "api/online-orders", HttpStatusCode.InternalServerError, """
                {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.",
                 "status":500,"detail":"Gözlənilməz xəta baş verdi.","instance":"POST /api/online-orders",
                 "traceId":"00-abc-01","ok":false,"error":"Gözlənilməz xəta baş verdi."}
                """);

        var result = await Client().PushOrderToErpAsync(order);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("NexusERP sifarişi rədd etdi (HTTP 500): Gözlənilməz xəta baş verdi.");
    }

    [Fact]
    public async Task A_500_without_a_body_still_fails_cleanly()
    {
        var (order, _) = await OrderAsync();
        _nexus.On(HttpMethod.Get, $"api/products/barcode/{Sku}", HttpStatusCode.OK, """{"ok":true,"product":{"id":"p-1"}}""")
            .On(HttpMethod.Post, "api/online-orders", HttpStatusCode.InternalServerError);

        (await Client().PushOrderToErpAsync(order)).Error.Should().Be("NexusERP sifarişi rədd etdi (HTTP 500): səbəb göstərilməyib");
    }

    [Fact]
    public async Task Without_a_configured_store_the_first_active_nexus_store_is_used()
    {
        var (order, _) = await OrderAsync();
        _nexus.On(HttpMethod.Get, "api/stores", HttpStatusCode.OK, """
                {"ok":true,"items":[{"id":"st-old","code":"MAG-001","name":"Bağlı","isActive":false},{"id":"st-2","code":"MAG-002","name":"Online","isActive":true}]}
                """)
            .On(HttpMethod.Get, $"api/products/barcode/{Sku}", HttpStatusCode.OK, """{"ok":true,"product":{"id":"p-1"}}""")
            .On(HttpMethod.Post, "api/online-orders", HttpStatusCode.OK, """{"ok":true,"id":"s-1","documentNumber":"STS-001"}""");

        (await Client(storeId: "").PushOrderToErpAsync(order)).Success.Should().BeTrue();

        JsonNode.Parse(_nexus.To(HttpMethod.Post, "api/online-orders").Single().Body!)!["storeId"]!.GetValue<string>().Should().Be("st-2");
    }

    [Fact]
    public async Task A_403_from_nexus_means_the_service_account_has_no_company()
    {
        var (order, _) = await OrderAsync();
        _nexus.On(HttpMethod.Get, "api/stores", HttpStatusCode.Forbidden, """{"ok":false,"error":"Şirkət seçilməyib"}""");

        var result = await Client(storeId: "").PushOrderToErpAsync(order);

        result.Error.Should().StartWith("NexusERP 403 qaytardı");
        _nexus.To(HttpMethod.Post, "api/online-orders").Should().BeEmpty();
    }

    // ---- Order cancel (DELETE api/online-orders/{number}) ----

    [Fact]
    public async Task Cancelling_succeeds_on_200_and_on_a_404_that_carries_nexus_error_body()
    {
        _nexus.On(HttpMethod.Delete, "api/online-orders/KHZ-AAAAA", HttpStatusCode.OK, """{"ok":true,"id":"s-1","status":"cancelled"}""")
            .On(HttpMethod.Delete, "api/online-orders/KHZ-BBBBB", HttpStatusCode.NotFound, """{"ok":false,"error":"Online sifariş tapılmadı."}""");

        (await Client().CancelOrderInErpAsync("KHZ-AAAAA")).Success.Should().BeTrue();
        (await Client().CancelOrderInErpAsync("KHZ-BBBBB")).Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_bodyless_404_means_a_missing_route_and_is_a_failure()
    {
        _nexus.On(HttpMethod.Delete, "api/online-orders/KHZ-CCCCC", HttpStatusCode.NotFound);

        var result = await Client().CancelOrderInErpAsync("KHZ-CCCCC");

        result.Should().Be(new Khazar.Application.Common.Interfaces.ErpPushResult(false, "NexusERP sifarişi ləğv etmədi (HTTP 404): səbəb göstərilməyib"));
    }

    [Fact]
    public async Task A_refused_cancellation_carries_nexus_reason()
    {
        _nexus.On(HttpMethod.Delete, "api/online-orders/KHZ-DDDDD", HttpStatusCode.Conflict, """{"ok":false,"error":"Satış eyni anda dəyişdirildi. Yenidən cəhd edin."}""");

        (await Client().CancelOrderInErpAsync("KHZ-DDDDD")).Error
            .Should().Be("NexusERP sifarişi ləğv etmədi (HTTP 409): Satış eyni anda dəyişdirildi. Yenidən cəhd edin.");
    }
}
