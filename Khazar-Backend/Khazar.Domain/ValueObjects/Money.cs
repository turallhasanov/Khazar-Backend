using Khazar.Domain.Common;
using Khazar.Domain.Enums;
using Khazar.Domain.Exceptions;

namespace Khazar.Domain.ValueObjects;

/// <summary>
/// An immutable monetary amount. Two instances are equal when both amount and currency match
/// (record equality provides the == and != operators).
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (!Enum.IsDefined(currency))
            throw new DomainException($"Unsupported currency: {currency}.");

        Amount = Guard.NotNegative(amount, nameof(Amount));
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(int quantity) =>
        new(Amount * Guard.NotNegative(quantity, nameof(quantity)), Currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator *(Money money, int quantity) => money.Multiply(quantity);

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
            throw new DomainException(
                $"Cannot combine amounts in different currencies: {Currency} and {other.Currency}.");
    }
}
