namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents a key-value application setting stored in the database.
/// Used for persisting user preferences, feature flags, and configuration.
/// </summary>
public class Setting : BaseEntity
{
    /// <summary>
    /// Unique setting key (e.g. "theme.mode", "notification.enabled").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Serialized setting value. Nullable to allow explicit null semantics.
    /// </summary>
    public string? Value { get; set; }
}
