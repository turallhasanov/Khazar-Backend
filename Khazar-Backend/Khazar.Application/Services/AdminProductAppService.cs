using System.Text.RegularExpressions;
using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.DTOs.Products;
using Khazar.Domain.Entities;

namespace Khazar.Application.Services;

public sealed partial class AdminProductAppService(IProductRepository productRepository) : IAdminProductAppService
{
    public const decimal MaxPrice = 100_000m;
    public const int MaxStock = 100_000;

    public async Task<IReadOnlyList<AdminProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await productRepository.GetAllForAdminAsync(cancellationToken);
        return products.Select(p => p.ToAdminDto()).ToList();
    }

    public async Task<AdminProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToAdminDto();

    public async Task<AdminProductDto> CreateAsync(CreateProductRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ValidateFields(dto);

        if (await productRepository.SlugExistsAsync(fields.Slug, cancellationToken: cancellationToken))
            throw SlugTaken();

        // The storefront needs a color and size to sell a product, so a product starts with at least one variant.
        var variants = (dto.Variants ?? []).Select(ValidateVariant).ToList();
        if (variants.Count == 0)
            throw new ValidationException("Add at least one variant (size, color, SKU).", "variants_required");
        if (variants.GroupBy(v => v.Sku).Any(g => g.Count() > 1))
            throw new ConflictException("Two variants use the same SKU.", "sku_taken");
        if (variants.GroupBy(v => (v.Color.ToUpperInvariant(), v.Size.ToUpperInvariant())).Any(g => g.Count() > 1))
            throw VariantExists();
        foreach (var variant in variants)
        {
            if (await productRepository.SkuExistsAsync(variant.Sku, cancellationToken))
                throw SkuTaken(variant.Sku);
        }

        var product = new Product(
            fields.Name, fields.Slug, fields.Description, fields.Price, fields.Fabric, fields.Category, fields.CoverImage,
            dto.IsFeatured);
        foreach (var variant in variants)
            product.AddVariant(variant.Size, variant.Color, variant.Sku, variant.StockQuantity);

        await productRepository.AddAsync(product, cancellationToken);
        return new ManagedProduct(product, 0, false, new HashSet<Guid>()).ToAdminDto();
    }

    public async Task<AdminProductDto> UpdateAsync(Guid id, SaveProductRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ValidateFields(dto);
        var managed = await LoadAsync(id, cancellationToken);

        if (await productRepository.SlugExistsAsync(fields.Slug, id, cancellationToken))
            throw SlugTaken();

        var product = managed.Product;
        product.UpdateDetails(fields.Name, fields.Slug, fields.Description, fields.Fabric, fields.Category, fields.CoverImage);
        if (product.Price != fields.Price)
            product.ChangePrice(fields.Price);
        if (product.IsFeatured != dto.IsFeatured)
            product.SetFeatured(dto.IsFeatured);

        await productRepository.UpdateAsync(product, cancellationToken);
        return managed.ToAdminDto();
    }

    public async Task<AdminProductDto> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var managed = await LoadAsync(id, cancellationToken);
        managed.Product.Archive();
        await productRepository.UpdateAsync(managed.Product, cancellationToken);
        return managed.ToAdminDto();
    }

    public async Task<AdminProductDto> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var managed = await LoadAsync(id, cancellationToken);
        managed.Product.Restore();
        await productRepository.UpdateAsync(managed.Product, cancellationToken);
        return managed.ToAdminDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var managed = await LoadAsync(id, cancellationToken);

        // Order lines keep the product and variant ids; deleting would leave them pointing at nothing.
        if (managed.HasOrders)
            throw new ConflictException(
                "This product appears in orders and cannot be deleted. Archive it instead.", "product_has_orders");

        await productRepository.DeleteAsync(managed.Product, cancellationToken);
    }

    public async Task<AdminProductDto> AddVariantAsync(
        Guid productId, CreateVariantRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var variant = ValidateVariant(dto);
        var managed = await LoadAsync(productId, cancellationToken);
        var product = managed.Product;

        if (product.Variants.Any(v => v.Color.Equals(variant.Color, StringComparison.OrdinalIgnoreCase)
                                      && v.Size.Equals(variant.Size, StringComparison.OrdinalIgnoreCase)))
            throw VariantExists();
        if (await productRepository.SkuExistsAsync(variant.Sku, cancellationToken))
            throw SkuTaken(variant.Sku);

        product.AddVariant(variant.Size, variant.Color, variant.Sku, variant.StockQuantity);
        await productRepository.UpdateAsync(product, cancellationToken);
        return managed.ToAdminDto();
    }

    public async Task<AdminProductDto> UpdateVariantStockAsync(
        Guid productId, Guid variantId, UpdateVariantStockRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateStock(dto.StockQuantity);
        var managed = await LoadAsync(productId, cancellationToken);

        var variant = managed.Product.Variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new NotFoundException(nameof(ProductVariant), variantId);
        if (variant.StockQuantity != dto.StockQuantity)
        {
            variant.SetStock(dto.StockQuantity);
            await productRepository.UpdateAsync(managed.Product, cancellationToken);
        }

        return managed.ToAdminDto();
    }

    public async Task<AdminProductDto> RemoveVariantAsync(Guid productId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var managed = await LoadAsync(productId, cancellationToken);
        if (managed.Product.Variants.All(v => v.Id != variantId))
            throw new NotFoundException(nameof(ProductVariant), variantId);

        if (managed.Product.Variants.Count == 1)
            throw new ConflictException(
                "A product needs at least one variant. Add another one first, or archive the product.", "last_variant");

        if (managed.OrderedVariantIds.Contains(variantId))
            throw new ConflictException(
                "This variant appears in orders and cannot be removed. Set its stock to 0 instead.", "variant_has_orders");

        managed.Product.RemoveVariant(variantId);
        await productRepository.UpdateAsync(managed.Product, cancellationToken);
        return managed.ToAdminDto();
    }

    private async Task<ManagedProduct> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await productRepository.GetForAdminByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(Product), id);

    private sealed record ProductFields(
        string Name, string Slug, string Description, string Category, string Fabric, decimal Price, string CoverImage);

    private sealed record VariantFields(string Size, string Color, string Sku, int StockQuantity);

    private static ProductFields ValidateFields(SaveProductRequestDto dto)
    {
        var name = Required(dto.Name, Product.MaxNameLength, "Name", "invalid_name");

        var slug = (dto.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (slug.Length == 0 || slug.Length > Product.MaxSlugLength || !SlugPattern().IsMatch(slug))
            throw new ValidationException(
                "Slug must use lower-case letters, digits and single hyphens (e.g. \"heavyweight-hoodie\").", "invalid_slug");

        var description = (dto.Description ?? string.Empty).Trim();
        if (description.Length > Product.MaxDescriptionLength)
            throw new ValidationException(
                $"Description must be at most {Product.MaxDescriptionLength} characters.", "invalid_description");

        var category = Required(dto.Category, Product.MaxCategoryLength, "Category", "invalid_category");
        var fabric = Required(dto.Fabric, Product.MaxFabricLength, "Fabric", "invalid_fabric");

        if (dto.Price <= 0 || dto.Price > MaxPrice || decimal.Round(dto.Price, 2) != dto.Price)
            throw new ValidationException(
                $"Price must be greater than 0 and at most {MaxPrice} with up to two decimals.", "invalid_price");

        var coverImage = (dto.CoverImage ?? string.Empty).Trim();
        if (coverImage.Length > Product.MaxCoverImageLength
            || !Uri.TryCreate(coverImage, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ValidationException("Cover image must be an absolute http(s) URL.", "invalid_cover_image");

        return new ProductFields(name, slug, description, category, fabric, dto.Price, coverImage);
    }

    private static VariantFields ValidateVariant(CreateVariantRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var size = Required(dto.Size, ProductVariant.MaxSizeLength, "Size", "invalid_size").ToUpperInvariant();
        var color = Required(dto.Color, ProductVariant.MaxColorLength, "Color", "invalid_color");

        var sku = (dto.Sku ?? string.Empty).Trim().ToUpperInvariant();
        if (sku.Length == 0 || sku.Length > ProductVariant.MaxSkuLength || !SkuPattern().IsMatch(sku))
            throw new ValidationException(
                "SKU must use letters, digits and hyphens (e.g. \"KHZ-HVHD-ANT-M\").", "invalid_sku");

        ValidateStock(dto.StockQuantity);
        return new VariantFields(size, color, sku, dto.StockQuantity);
    }

    private static void ValidateStock(int quantity)
    {
        if (quantity is < 0 or > MaxStock)
            throw new ValidationException($"Stock must be between 0 and {MaxStock}.", "invalid_stock");
    }

    private static string Required(string? value, int maxLength, string field, string errorCode)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
            throw new ValidationException($"{field} is required and must be at most {maxLength} characters.", errorCode);
        return trimmed;
    }

    private static ConflictException SlugTaken() => new("Another product already uses this slug.", "slug_taken");

    private static ConflictException SkuTaken(string sku) => new($"SKU '{sku}' is already used by another variant.", "sku_taken");

    private static ConflictException VariantExists() =>
        new("This product already has a variant with this color and size.", "variant_exists");

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[A-Z0-9]+(?:-[A-Z0-9]+)*$")]
    private static partial Regex SkuPattern();
}
