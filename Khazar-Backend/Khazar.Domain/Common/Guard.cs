using Khazar.Domain.Exceptions;

namespace Khazar.Domain.Common;

internal static class Guard
{
    public static string NotEmpty(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} cannot be empty.");
        return value.Trim();
    }

    public static decimal NotNegative(decimal value, string name)
    {
        if (value < 0)
            throw new DomainException($"{name} cannot be negative.");
        return value;
    }

    public static int NotNegative(int value, string name)
    {
        if (value < 0)
            throw new DomainException($"{name} cannot be negative.");
        return value;
    }

    public static int Positive(int value, string name)
    {
        if (value <= 0)
            throw new DomainException($"{name} must be greater than zero.");
        return value;
    }

    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{name} cannot be empty.");
        return value;
    }
}
