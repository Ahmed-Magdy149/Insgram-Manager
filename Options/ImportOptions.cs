namespace Insgram_Manager.Options;

/// <summary>
/// Configuration options for the import module.
/// Bound from the "Import" section in appsettings.json.
/// </summary>
public sealed class ImportOptions
{
    public const string SectionName = "Import";

    public int MaxFileSizeMb { get; set; } = 50;

    public string[] AllowedExtensions { get; set; } = [".json", ".csv", ".html"];

    public int BatchSize { get; set; } = 100;

    public bool SkipDuplicates { get; set; } = true;
}
