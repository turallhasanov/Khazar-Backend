using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.ExternalServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Khazar.WebAPI.Tests.NexusErp;

/// <summary>NexusERP pushes "Usaq Hoodie (S)", "(M)" ...: they become sizes of one "Usaq Hoodie" card, each with its own SKU.</summary>
public sealed class ProductAggregationTests
{
    private readonly Mock<IProductRepository> _products = new();

    public ProductAggregationTests() =>
        _products.Setup(p => p.GetByVariantSkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    private NexusProductSyncService Service()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_products.Object);
        services.AddSingleton(Mock.Of<ICategoryRepository>(c =>
            c.GetAllAsync(It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<Category>>(new List<Category> { Category })));
        var provider = services.BuildServiceProvider();

        return new NexusProductSyncService(
            Mock.Of<INexusErpClient>(), provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new NexusErpOptions { SyncEnabled = true, PlaceholderImageUrl = "/images/placeholder.jpg" }),
            NullLogger<NexusProductSyncService>.Instance);
    }

    private static readonly Category Category = new("Kids", "Uşaq", "usaq", 0);

    private static Product Hoodie(string size)
    {
        var product = new Product("Usaq Hoodie", "usaq-hoodie", "", 20m, "pambıq", Category, "/images/h.jpg");
        product.AddVariant(size, "Standart", "SKU-" + size, 1);
        return product;
    }

    private static NexusProductPush Push(string name, string sku, decimal price = 20m, int stock = 4) =>
        new(sku, name, "Kids", price, stock, true, null);

    [Fact]
    public async Task A_second_size_is_added_to_the_existing_product()
    {
        var hoodie = Hoodie("S");
        _products.Setup(p => p.GetActiveByNameAsync("Usaq Hoodie", It.IsAny<CancellationToken>())).ReturnsAsync(hoodie);

        var result = await Service().PushProductAsync(Push("Usaq Hoodie (M)", "SKU-M"));

        result.Outcome.Should().Be("created");
        result.Slug.Should().Be("usaq-hoodie");
        hoodie.Variants.Select(v => (v.Size, v.SKU, v.StockQuantity)).Should().BeEquivalentTo(
            new[] { ("S", "SKU-S", 1), ("M", "SKU-M", 4) });
        _products.Verify(p => p.UpdateAsync(hoodie, It.IsAny<CancellationToken>()), Times.Once);
        _products.Verify(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
        hoodie.Price.Should().Be(20m);
    }

    [Fact]
    public async Task A_different_price_does_not_change_the_products_price()
    {
        var hoodie = Hoodie("S");
        _products.Setup(p => p.GetActiveByNameAsync("Usaq Hoodie", It.IsAny<CancellationToken>())).ReturnsAsync(hoodie);

        await Service().PushProductAsync(Push("Usaq Hoodie (L)", "SKU-L", price: 35m));

        hoodie.Price.Should().Be(20m);
        hoodie.Variants.Should().Contain(v => v.SKU == "SKU-L");
    }

    [Fact]
    public async Task Without_an_existing_product_a_new_one_is_created_with_the_first_size()
    {
        Product? created = null;
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => created = product)
            .ReturnsAsync((Product product, CancellationToken _) => product);

        var result = await Service().PushProductAsync(Push("Usaq Hoodie (S)", "SKU-S"));

        result.Outcome.Should().Be("created");
        created!.Name.Should().Be("Usaq Hoodie");
        created.Variants.Should().ContainSingle().Which.Size.Should().Be("S");
    }

    [Fact]
    public async Task A_size_the_product_already_has_becomes_its_own_product_instead_of_clashing()
    {
        var hoodie = Hoodie("M");
        _products.Setup(p => p.GetActiveByNameAsync("Usaq Hoodie", It.IsAny<CancellationToken>())).ReturnsAsync(hoodie);
        _products.Setup(p => p.SlugExistsAsync("usaq-hoodie", It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Product? created = null;
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => created = product)
            .ReturnsAsync((Product product, CancellationToken _) => product);

        await Service().PushProductAsync(Push("Usaq Hoodie (M)", "SKU-M2"));

        hoodie.Variants.Should().ContainSingle();
        created!.Variants.Single().SKU.Should().Be("SKU-M2");
    }
}
