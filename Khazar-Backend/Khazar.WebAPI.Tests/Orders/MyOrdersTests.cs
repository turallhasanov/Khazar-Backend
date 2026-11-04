using System.Net;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Services;
using Khazar.Domain.Entities;
using Khazar.Domain.ValueObjects;
using Khazar.WebAPI.Tests.TestSupport;

namespace Khazar.WebAPI.Tests.Orders;

/// <summary>"My orders": a customer sees only their own orders, with photos, and the endpoints need a sign-in.</summary>
public sealed class MyOrdersTests(KhazarApiFactory factory) : IClassFixture<KhazarApiFactory>
{
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly Mock<IOrderRepository> _orders = new();

    private OrderAppService Service() => new(
        _orders.Object, Mock.Of<IProductRepository>(), Mock.Of<ICurrentUserService>(),
        Mock.Of<IOrderErpSyncQueue>(), Mock.Of<IBackgroundEmailQueue>());

    private static (Order Order, Product Product) PlacedOrder(Guid? userId)
    {
        var product = new Product("Yun palto", "yun-palto", "Təsvir", 149.90m, "yun", new Category("Coat", "Palto", "palto", 0), "/images/coat.jpg");
        var variant = product.AddVariant("M", "Qara", "KHZ-1", 5);
        var order = new Order("Ali Aliyev", "ali@test.az", new Address("Nizami 1", "Bakı", "AZ1000", "Azerbaijan"), userId);
        order.AddItem(variant, product.Name, 149.90m, 2);
        return (order, product);
    }

    [Fact]
    public async Task GetMyOrdersAsync_returns_the_orders_with_status_total_lines_and_cover_photo()
    {
        var (order, product) = PlacedOrder(Alice);
        _orders.Setup(r => r.GetByUserIdAsync(Alice, It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        _orders.Setup(r => r.GetCoverImagesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [product.Id] = "/images/coat.jpg" });

        var result = await Service().GetMyOrdersAsync(Alice);

        var mine = result.Should().ContainSingle().Subject;
        mine.OrderNumber.Should().Be(order.OrderNumber);
        mine.Status.Should().Be("Pending");
        mine.TotalAmount.Should().Be(299.80m);
        mine.ItemCount.Should().Be(2);
        mine.ShippingAddress.Should().Contain("Nizami 1");
        var line = mine.Items.Should().ContainSingle().Subject;
        (line.Name, line.Quantity, line.UnitPrice, line.ImageUrl).Should().Be(("Yun palto", 2, 149.90m, "/images/coat.jpg"));
    }

    [Fact]
    public async Task GetMyOrderAsync_hides_someone_elses_order_and_accepts_a_hash_prefix()
    {
        var (order, _) = PlacedOrder(Alice);
        _orders.Setup(r => r.GetByNumberAsync(order.OrderNumber, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _orders.Setup(r => r.GetCoverImagesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        (await Service().GetMyOrderAsync(Bob, order.OrderNumber)).Should().BeNull();
        (await Service().GetMyOrderAsync(Alice, "#" + order.OrderNumber.ToLowerInvariant()))!.OrderNumber.Should().Be(order.OrderNumber);
    }

    [Theory]
    [InlineData("/api/orders/my-orders")]
    [InlineData("/api/orders/my-orders/KHZ-ABCDE")]
    public async Task The_endpoints_need_a_signed_in_customer(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
