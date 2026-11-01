using System.Text;
using System.Text.RegularExpressions;
using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Khazar.Infrastructure.ExternalServices;

/// <summary>
/// Pulls the NexusERP catalog into Khazar. A Khazar variant SKU is the barcode of its NexusERP product. NexusERP has no
/// variants, so a product it knows but Khazar does not becomes one Khazar product with a single "Standart" variant;
/// admins add photos, sizes and colors afterwards. Nothing is archived or deleted when NexusERP drops a product.
/// Each product is saved in its own scope, so one that fails (e.g. a checkout changed its stock mid-run) cannot
/// leave a broken change behind for the next ones.
/// </summary>
public sealed partial class NexusProductSyncService(
    INexusErpClient erpClient,
    IServiceScopeFactory scopeFactory,
    IOptions<NexusErpOptions> options,
    ILogger<NexusProductSyncService> logger) : INexusProductSyncService
{
    private const string DefaultVariantValue = "Standart";
    private const string DefaultFabric = "Qeyd olunmayıb";
    private const string DefaultCategoryName = "NexusERP";

    // Where the photos NexusERP sends are stored (see IntegrationController).
    private const string UploadedPhotoPrefix = "/uploads/products/";

    public async Task<NexusProductSyncResult> PullProductsAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.SyncEnabled)
            throw new ExternalServiceException("NexusERP sync is disabled (NexusErp:SyncEnabled).", "nexus_sync_disabled");

        var snapshots = await erpClient.GetProductsAsync(cancellationToken)
            ?? throw new ExternalServiceException("NexusERP products could not be read. Check the NexusERP settings.", "nexus_unreachable");

        int created = 0, updated = 0, unchanged = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var snapshot in snapshots.GroupBy(s => s.Sku).Select(g => g.First()))
        {
            if (snapshot.Sku.Length > ProductVariant.MaxSkuLength || !SkuPattern().IsMatch(snapshot.Sku))
            {
                skipped++;
                errors.Add($"{snapshot.Sku}: barcode is not a valid SKU (letters, digits and hyphens).");
                continue;
            }

            try
            {
                switch (await UpsertAsync(snapshot, cancellationToken))
                {
                    case Outcome.Created: created++; break;
                    case Outcome.Updated: updated++; break;
                    default: unchanged++; break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
                errors.Add($"{snapshot.Sku}: {ex.Message}");
                logger.LogWarning(ex, "NexusERP product {Sku} could not be synchronized.", snapshot.Sku);
            }
        }

        logger.LogInformation(
            "NexusERP catalog pull: {Fetched} fetched, {Created} created, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped.",
            snapshots.Count, created, updated, unchanged, skipped);

        return new NexusProductSyncResult(snapshots.Count, created, updated, unchanged, skipped, errors);
    }

    public async Task<NexusProductPushResult> PushProductAsync(NexusProductPush push, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(push);

        var sku = (push.Sku ?? string.Empty).Trim().ToUpperInvariant();
        if (sku.Length == 0 || sku.Length > ProductVariant.MaxSkuLength || !SkuPattern().IsMatch(sku))
            throw new ValidationException(
                "The barcode must be letters, digits and hyphens (it becomes the product's SKU).", "invalid_sku");

        var name = (push.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > Product.MaxNameLength)
            throw new ValidationException($"The product name is required (at most {Product.MaxNameLength} characters).", "invalid_name");

        if (push.SalePrice < 0)
            throw new ValidationException("The price cannot be negative.", "invalid_price");

        var stock = Math.Max(0, push.Stock);

        await using var scope = scopeFactory.CreateAsyncScope();
        var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var existing = (await productRepository.GetByVariantSkusAsync([sku], cancellationToken)).FirstOrDefault();

        if (existing is null)
        {
            // Not shown on the website, and not there yet: nothing to add.
            if (!push.IsPublished)
                return new NexusProductPushResult("skipped", null, null, []);

            var category = await FindOrCreateCategoryAsync(scope.ServiceProvider, push.CategoryName, cancellationToken);
            var slug = await UniqueSlugAsync(productRepository, name, sku, cancellationToken);

            var product = new Product(
                name, slug, string.Empty, push.SalePrice, DefaultFabric, category,
                push.ImageUrl ?? options.Value.PlaceholderImageUrl);
            product.AddVariant(DefaultVariantValue, DefaultVariantValue, sku, stock);

            await productRepository.AddAsync(product, cancellationToken);
            logger.LogInformation("NexusERP product {Sku} was added to the catalog as {Slug}.", sku, slug);
            return new NexusProductPushResult("created", slug, push.ImageUrl, []);
        }

        var variant = existing.Variants.First(v => v.SKU == sku);
        var changed = false;

        if (variant.StockQuantity != stock)
        {
            variant.SetStock(stock);
            changed = true;
        }

        if (existing.Price != push.SalePrice)
        {
            existing.ChangePrice(push.SalePrice);
            changed = true;
        }

        // NexusERP names its products "Name (Color / Size)", so an edit there can change the name. Only a product a push
        // (or the pull) created follows it: its one variant is the default "Standart" one. A Khazar product with real
        // sizes and colors is the same product for all of them, and one variant's name must not rename it.
        if (existing.Name != name && existing.Variants.Count == 1
            && variant.Size.Equals(DefaultVariantValue, StringComparison.OrdinalIgnoreCase)
            && variant.Color.Equals(DefaultVariantValue, StringComparison.OrdinalIgnoreCase))
        {
            existing.UpdateDetails(name, existing.Slug, existing.Description, existing.Fabric, existing.Category);
            changed = true;
        }

        if (push.IsPublished != existing.IsActive)
        {
            if (push.IsPublished)
                existing.Restore();
            else
                existing.Archive();
            changed = true;
        }

        var replaced = new List<string>();
        if (push.ImageUrl is not null)
        {
            // The new photo goes in front. An earlier NexusERP photo (stored under /uploads/products) is superseded by it,
            // so saving the same product again never piles up copies; photos from the admin panel (/images/...) and the
            // placeholder it replaces are not NexusERP's: the first stays, the second goes.
            var placeholder = options.Value.PlaceholderImageUrl;
            var ordered = existing.Images.OrderBy(i => i.SortOrder).ToList();
            replaced.AddRange(ordered.Where(i => i.Url.StartsWith(UploadedPhotoPrefix, StringComparison.Ordinal)).Select(i => i.Url));

            var gallery = new List<(string Url, string? Color)> { (push.ImageUrl, null) };
            gallery.AddRange(ordered
                .Where(i => i.Url != placeholder && !i.Url.StartsWith(UploadedPhotoPrefix, StringComparison.Ordinal))
                .Select(i => (i.Url, i.Color)));
            existing.ReplaceImages(gallery.Take(Product.MaxImages).ToList());
            changed = true;
        }

        if (changed)
            await productRepository.UpdateAsync(existing, cancellationToken);

        return new NexusProductPushResult(changed ? "updated" : "unchanged", existing.Slug, push.ImageUrl, replaced);
    }

    private async Task<Outcome> UpsertAsync(NexusProductSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        var existing = (await productRepository.GetByVariantSkusAsync([snapshot.Sku], cancellationToken)).FirstOrDefault();
        if (existing is not null)
        {
            var variant = existing.Variants.First(v => v.SKU == snapshot.Sku);
            var changed = false;

            if (variant.StockQuantity != snapshot.Stock)
            {
                variant.SetStock(snapshot.Stock);
                changed = true;
            }

            if (existing.Price != snapshot.SalePrice)
            {
                existing.ChangePrice(snapshot.SalePrice);
                changed = true;
            }

            if (!changed)
                return Outcome.Unchanged;

            await productRepository.UpdateAsync(existing, cancellationToken);
            return Outcome.Updated;
        }

        var category = await FindOrCreateCategoryAsync(scope.ServiceProvider, snapshot.CategoryName, cancellationToken);
        var slug = await UniqueSlugAsync(productRepository, snapshot.Name, snapshot.Sku, cancellationToken);

        var product = new Product(
            snapshot.Name, slug, string.Empty, snapshot.SalePrice, DefaultFabric, category,
            options.Value.PlaceholderImageUrl);
        product.AddVariant(DefaultVariantValue, DefaultVariantValue, snapshot.Sku, snapshot.Stock);

        await productRepository.AddAsync(product, cancellationToken);
        return Outcome.Created;
    }

    private static async Task<Category> FindOrCreateCategoryAsync(
        IServiceProvider services, string? name, CancellationToken cancellationToken)
    {
        var categoryRepository = services.GetRequiredService<ICategoryRepository>();
        var wanted = name ?? DefaultCategoryName;

        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        var match = categories.FirstOrDefault(c =>
            c.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase) || c.Label.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        // GetAllAsync returns untracked rows; a product built on one would make EF insert the category a second time.
        // The tracked copy is what the new product must point at.
        if (match is not null)
            return await categoryRepository.GetByIdAsync(match.Id, cancellationToken) ?? match;

        var slug = Slugify(wanted);
        var category = new Category(wanted, wanted, slug.Length == 0 ? "nexuserp" : slug, categories.Count);
        return await categoryRepository.AddAsync(category, cancellationToken);
    }

    private static async Task<string> UniqueSlugAsync(
        IProductRepository productRepository, string name, string sku, CancellationToken cancellationToken)
    {
        var slug = Slugify(name);
        if (slug.Length == 0)
            slug = "mehsul";
        if (slug.Length > Product.MaxSlugLength)
            slug = slug[..Product.MaxSlugLength].TrimEnd('-');

        if (!await productRepository.SlugExistsAsync(slug, cancellationToken: cancellationToken))
            return slug;

        var suffix = "-" + sku.ToLowerInvariant();
        return slug[..Math.Min(slug.Length, Product.MaxSlugLength - suffix.Length)].TrimEnd('-') + suffix;
    }

    /// <summary>Lower-case ASCII slug; Azerbaijani letters are transliterated.</summary>
    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            builder.Append(ch switch
            {
                'ə' => 'e', 'ı' => 'i', 'ö' => 'o', 'ü' => 'u', 'ş' => 's', 'ç' => 'c', 'ğ' => 'g',
                _ => ch,
            });
        }

        return Regex.Replace(builder.ToString(), "[^a-z0-9]+", "-").Trim('-');
    }

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex SkuPattern();

    private enum Outcome { Created, Updated, Unchanged }
}
