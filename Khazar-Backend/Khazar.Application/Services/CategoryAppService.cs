using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.DTOs.Categories;

namespace Khazar.Application.Services;

public sealed class CategoryAppService(ICategoryRepository categoryRepository) : ICategoryAppService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(c => c.ToDto()).ToList();
    }
}
