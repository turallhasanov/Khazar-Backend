namespace Khazar.Application.DTOs.Orders;

public sealed record CustomerDto
{
    /// <summary>Customer segment; the web checkout always sends "B2C".</summary>
    public string Type { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}
