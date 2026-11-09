using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Khazar.Infrastructure.ExternalServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Khazar.WebAPI.Tests.NexusErp;

/// <summary>Photos NexusERP sends with a product: the first is the cover, the rest follow it in the gallery.</summary>
public sealed class ProductGalleryTests
{
    private const string Placeholder = "/images/placeholder.jpg";
    private static readonly Category Kids = new("Kids", "Uşaq", "usaq", 0);

    private readonly Mock<IProductRepository> _products = new();

    public ProductGalleryTests() =>
        _products.Setup(p => p.GetByVariantSkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    private NexusProductSyncService Service()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_products.Object);
        services.AddSingleton(Mock.Of<ICategoryRepository>(c =>
            c.GetAllAsync(It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<Category>>(new List<Category> { Kids })));

        return new NexusProductSyncService(
            Mock.Of<INexusErpClient>(), services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new NexusErpOptions { SyncEnabled = true, PlaceholderImageUrl = Placeholder }),
            NullLogger<NexusProductSyncService>.Instance);
    }

    private static NexusProductPush Push(string? main, params string[] gallery) =>
        new("SKU-1", "Usaq Hoodie", "Kids", 20m, 3, true, main) { GalleryUrls = gallery };

    private Product Capture()
    {
        Product? created = null;
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => created = product)
            .ReturnsAsync((Product product, CancellationToken _) => product);
        return created!;
    }

    [Fact]
    public async Task A_new_product_gets_the_main_photo_first_then_the_gallery()
    {
        Product? created = null;
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => created = product)
            .ReturnsAsync((Product product, CancellationToken _) => product);

        await Service().PushProductAsync(Push("/uploads/products/a.png", "/uploads/products/b.png", "/uploads/products/c.png"));

        created!.CoverImage.Should().Be("/uploads/products/a.png");
        created.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).Should().Equal(
            "/uploads/products/a.png", "/uploads/products/b.png", "/uploads/products/c.png");
    }

    [Fact]
    public async Task Without_any_photo_the_placeholder_is_used()
    {
        Product? created = null;
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => created = product)
            .ReturnsAsync((Product product, CancellationToken _) => product);

        await Service().PushProductAsync(Push(null));

        created!.CoverImage.Should().Be(Placeholder);
        created.Images.Should().ContainSingle();
    }

    [Fact]
    public async Task Gallery_photos_without_a_main_one_follow_the_photos_the_product_has()
    {
        var existing = new Product("Usaq Hoodie", "usaq-hoodie", "", 20m, "pambıq", Kids, "/images/admin.jpg");
        existing.AddVariant("Standart", "Standart", "SKU-1", 1);
        _products.Setup(p => p.GetByVariantSkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var result = await Service().PushProductAsync(Push(null, "/uploads/products/b.png", "/uploads/products/c.png"));

        existing.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).Should().Equal(
            "/images/admin.jpg", "/uploads/products/b.png", "/uploads/products/c.png");
        result.ReplacedImageUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task A_new_main_photo_goes_in_front_and_replaces_the_earlier_uploaded_one()
    {
        var existing = new Product("Usaq Hoodie", "usaq-hoodie", "", 20m, "pambıq", Kids, "/uploads/products/old.png");
        existing.AddVariant("Standart", "Standart", "SKU-1", 1);
        _products.Setup(p => p.GetByVariantSkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var result = await Service().PushProductAsync(Push("/uploads/products/new.png", "/uploads/products/extra.png"));

        existing.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).Should().Equal("/uploads/products/new.png", "/uploads/products/extra.png");
        result.ReplacedImageUrls.Should().Equal("/uploads/products/old.png");
    }

    [Fact]
    public async Task Photos_beyond_the_gallery_limit_are_reported_for_deletion()
    {
        var urls = Enumerable.Range(0, Product.MaxImages + 2).Select(i => $"/uploads/products/{i}.png").ToArray();
        Capture();

        var result = await Service().PushProductAsync(Push(urls[0], urls[1..]));

        result.ReplacedImageUrls.Should().Equal(urls.Skip(Product.MaxImages));
    }
}
