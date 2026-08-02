namespace Insgram_Manager.Domain.ValueObjects;

/// <summary>
/// Value Object representing an Instagram username.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public readonly record struct Username
{
    public string Value { get; }

    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Username cannot be empty.", nameof(value));

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
