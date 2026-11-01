namespace Khazar.WebAPI.Routing;

/// <summary>
/// Lowercases route tokens such as [controller], so ProductsController is routed, documented in Swagger
/// and linked in Location headers as /api/products instead of /api/Products.
/// </summary>
public sealed class LowercaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) => value?.ToString()?.ToLowerInvariant();
}
