namespace ComptaShield.SharedKernel.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount with currency.
/// Uses banker's rounding (MidpointRounding.ToEven) throughout.
/// NEVER use float/double for money — only decimal.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        Amount = decimal.Round(amount, 4, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>Creates a zero-value Money in the given currency.</summary>
    public static Money Zero(string currency) => new(0m, currency);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Cannot perform arithmetic on different currencies: {Currency} vs {other.Currency}.");
    }

    public static Money operator +(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
        => new(money.Amount * multiplier, money.Currency);

    public static Money operator *(decimal multiplier, Money money)
        => money * multiplier;

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0m)
            throw new DivideByZeroException("Cannot divide money by zero.");
        return new(money.Amount / divisor, money.Currency);
    }

    public static Money operator -(Money money)
        => new(-money.Amount, money.Currency);

    public static bool operator >(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        left.EnsureSameCurrency(right);
        return left.Amount <= right.Amount;
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public Money Round(int decimals = 2)
        => new(decimal.Round(Amount, decimals, MidpointRounding.ToEven), Currency);

    public override string ToString() => $"{Amount:N4} {Currency}";
}
