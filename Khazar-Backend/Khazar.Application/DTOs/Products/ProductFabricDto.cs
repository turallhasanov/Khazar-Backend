namespace Khazar.Application.DTOs.Products;

public sealed record ProductFabricDto
{
    public string Material { get; init; } = string.Empty;
    public string? Weave { get; init; }
    public IReadOnlyList<string> Care { get; init; } = [];
}
