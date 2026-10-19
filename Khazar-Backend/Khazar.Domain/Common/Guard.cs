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

    /// <summary>Validates the basic shape of an email address and returns it trimmed and lower-cased.</summary>
    public static string Email(string? value, string name)
    {
        var email = NotEmpty(value, name);
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
            throw new DomainException("Invalid email address.");

        return email.ToLowerInvariant();
    }

    public static Guid NotEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{name} cannot be empty.");
        return value;
    }
}
