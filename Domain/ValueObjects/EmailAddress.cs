namespace Insgram_Manager.Domain.ValueObjects;

/// <summary>
/// Value Object representing a validated email address.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public readonly record struct EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email address cannot be empty.", nameof(value));

        if (!value.Contains('@'))
            throw new ArgumentException("Email address is not valid.", nameof(value));

        Value = value.Trim().ToLowerInvariant();
    }

    public override string ToString() => Value;
}
