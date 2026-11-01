using System.Text.RegularExpressions;
using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.DTOs.Categories;
using Khazar.Domain.Entities;

namespace Khazar.Application.Services;

public sealed partial class AdminCategoryAppService(ICategoryRepository categoryRepository) : IAdminCategoryAppService
{
    public const int MaxSortOrder = 10_000;

    public async Task<IReadOnlyList<AdminCategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllForAdminAsync(cancellationToken);
        return categories.Select(c => c.ToAdminDto()).ToList();
    }

    public async Task<AdminCategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToAdminDto();

    public async Task<AdminCategoryDto> CreateAsync(SaveCategoryRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ValidateFields(dto);
        await EnsureUniqueAsync(fields, null, cancellationToken);

        var category = new Category(fields.Name, fields.Label, fields.Slug, fields.SortOrder);
        await categoryRepository.AddAsync(category, cancellationToken);
        return new ManagedCategory(category, 0).ToAdminDto();
    }

    public async Task<AdminCategoryDto> UpdateAsync(Guid id, SaveCategoryRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ValidateFields(dto);
        var managed = await LoadAsync(id, cancellationToken);
        await EnsureUniqueAsync(fields, id, cancellationToken);

        managed.Category.UpdateDetails(fields.Name, fields.Label, fields.Slug, fields.SortOrder);
        await categoryRepository.UpdateAsync(managed.Category, cancellationToken);
        return managed.ToAdminDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var managed = await LoadAsync(id, cancellationToken);

        // Every product needs a category; moving them is left to the admin so none lands somewhere unintended.
        if (managed.ProductCount > 0)
            throw new ConflictException(
                $"This category still has {managed.ProductCount} product(s). Move them to another category first.",
                "category_has_products");

        await categoryRepository.DeleteAsync(managed.Category, cancellationToken);
    }

    private async Task<ManagedCategory> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await categoryRepository.GetForAdminByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException(nameof(Category), id);

    private async Task EnsureUniqueAsync(CategoryFields fields, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await categoryRepository.NameExistsAsync(fields.Name, exceptId, cancellationToken))
            throw new ConflictException("Another category already uses this name.", "category_name_taken");
        if (await categoryRepository.SlugExistsAsync(fields.Slug, exceptId, cancellationToken))
            throw new ConflictException("Another category already uses this slug.", "category_slug_taken");
    }

    private sealed record CategoryFields(string Name, string Label, string Slug, int SortOrder);

    private static CategoryFields ValidateFields(SaveCategoryRequestDto dto)
    {
        var name = Required(dto.Name, Category.MaxNameLength, "Name", "invalid_name");
        var label = Required(dto.Label, Category.MaxLabelLength, "Label", "invalid_label");

        var slug = (dto.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (slug.Length == 0 || slug.Length > Category.MaxSlugLength || !SlugPattern().IsMatch(slug))
            throw new ValidationException(
                "Slug must use lower-case letters, digits and single hyphens (e.g. \"palto\").", "invalid_slug");

        if (dto.SortOrder is < 0 or > MaxSortOrder)
            throw new ValidationException($"Sort order must be between 0 and {MaxSortOrder}.", "invalid_sort_order");

        return new CategoryFields(name, label, slug, dto.SortOrder);
    }

    private static string Required(string? value, int maxLength, string field, string errorCode)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
            throw new ValidationException($"{field} is required and must be at most {maxLength} characters.", errorCode);
        return trimmed;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
