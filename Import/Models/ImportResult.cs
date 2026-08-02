namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents the result of an import operation.
/// Carries parsed profiles and relationships extracted from the source file.
/// </summary>
public sealed class ImportResult
{
    public bool Success { get; init; }

    public int TotalRecords { get; init; }

    public int ImportedRecords { get; init; }

    public int SkippedRecords { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<InstagramProfileModel> Profiles { get; init; } = [];

    public IReadOnlyList<InstagramRelationshipModel> Relationships { get; init; } = [];

    public string? Message { get; init; }
}
