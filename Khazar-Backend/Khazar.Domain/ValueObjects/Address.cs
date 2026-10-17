using Khazar.Domain.Common;

namespace Khazar.Domain.ValueObjects;

/// <summary>An immutable postal address. Equality is based on all components.</summary>
public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string ZipCode { get; }
    public string Country { get; }

    public Address(string street, string city, string zipCode, string country)
    {
        Street = Guard.NotEmpty(street, nameof(Street));
        City = Guard.NotEmpty(city, nameof(City));
        ZipCode = Guard.NotEmpty(zipCode, nameof(ZipCode));
        Country = Guard.NotEmpty(country, nameof(Country));
    }

    public override string ToString() => $"{Street}, {City} {ZipCode}, {Country}";
}
