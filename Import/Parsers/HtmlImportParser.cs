using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Import.Parsers;

/// <summary>
/// Parser for HTML-format Instagram data exports.
/// Uses HtmlAgilityPack to extract profile information from Instagram HTML export files.
/// Supports parsing of profile pages containing username, follower counts, and bio.
/// </summary>
public sealed partial class HtmlImportParser : IInstagramImportParser
{
    private readonly ILogger<HtmlImportParser> _logger;

    public HtmlImportParser(ILogger<HtmlImportParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(ImportFormat format) => format == ImportFormat.Html;

    public Task<ImportResult> ParseAsync(ImportRequest request)
    {
        var errors = new List<string>();
        var profiles = new List<InstagramProfileModel>();
        var relationships = new List<InstagramRelationshipModel>();

        try
        {
            if (request.FileStream is null)
            {
                errors.Add("File stream is null.");
                return Task.FromResult(BuildResult(false, 0, 0, 0, errors, [], []));
            }

            using var reader = new StreamReader(request.FileStream);
            var html = reader.ReadToEnd();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            _logger.LogDebug("Parsing HTML file: {FileName} (length: {Length})", request.FileName, html.Length);

            var relationshipType = DetectRelationshipType(request.FileName);

            if (relationshipType is not null)
            {
                relationships = ParseRelationships(doc, request.FileName, relationshipType.Value, errors);
                _logger.LogInformation("Parsed {Count} relationships ({Type}) from {FileName}", relationships.Count, relationshipType, request.FileName);
            }
            else
            {
                profiles = ParseProfiles(doc, request.FileName, errors);
                _logger.LogInformation("Parsed {Count} profiles from {FileName}", profiles.Count, request.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse HTML file: {FileName}", request.FileName);
            errors.Add($"Parse error: {ex.Message}");
        }

        var total = Math.Max(profiles.Count, relationships.Count);
        return Task.FromResult(BuildResult(
            success: errors.Count == 0,
            totalRecords: total,
            importedRecords: total,
            skippedRecords: 0,
            errors: errors,
            profiles: profiles,
            relationships: relationships));
    }

    /// <summary>
    /// Extracts Instagram profile models from an HTML document.
    /// Instagram data exports contain profile information in structured HTML.
    /// This parser looks for common patterns in Instagram export HTML files.
    /// </summary>
    private List<InstagramProfileModel> ParseProfiles(HtmlDocument doc, string sourceFileName, List<string> errors)
    {
        var profiles = new List<InstagramProfileModel>();

        // Instagram exports typically contain profile sections with class-based selectors.
        // We look for common patterns: profile headers, username links, and metadata lists.
        var profileNodes = doc.DocumentNode.SelectNodes("//section[contains(@class, 'profile')]") ??
                           doc.DocumentNode.SelectNodes("//div[contains(@class, 'profile')]") ??
                           doc.DocumentNode.SelectNodes("//div[@class='content']");

        if (profileNodes is null || profileNodes.Count == 0)
        {
            // Fallback: try to extract a single profile from the page structure
            var profile = ParseSingleProfile(doc, sourceFileName, errors);
            if (profile is not null)
                profiles.Add(profile);
        }
        else
        {
            foreach (var node in profileNodes)
            {
                var profile = ExtractProfileFromNode(node, sourceFileName);
                if (profile is not null)
                    profiles.Add(profile);
            }
        }

        return profiles;
    }

    private InstagramProfileModel? ParseSingleProfile(HtmlDocument doc, string sourceFileName, List<string> errors)
    {
        var username = ExtractUsername(doc);
        if (string.IsNullOrWhiteSpace(username))
        {
            errors.Add($"No username found in {sourceFileName}");
            return null;
        }

        var profile = new InstagramProfileModel
        {
            Username = username,
            FullName = ExtractText(doc, "//h1") ?? ExtractText(doc, "//div[contains(@class,'full_name')]"),
            Biography = ExtractText(doc, "//div[contains(@class,'biography')]") ??
                       ExtractText(doc, "//div[contains(@class,'bio')]"),
            ProfilePictureUrl = ExtractAttribute(doc, "//img[contains(@class,'profile')]", "src"),
            FollowerCount = ExtractCount(doc, "followers"),
            FollowingCount = ExtractCount(doc, "following"),
            PostCount = ExtractCount(doc, "posts"),
            IsVerified = doc.DocumentNode.SelectSingleNode("//*[contains(@class,'verified')]") is not null,
            IsPrivate = doc.DocumentNode.SelectSingleNode("//*[contains(text(),'private') or contains(text(),'Private')]") is not null
        };

        return profile;
    }

    private InstagramProfileModel? ExtractProfileFromNode(HtmlNode node, string sourceFileName)
    {
        var username = ExtractTextFromNode(node, ".//a[contains(@href,'/')]") ??
                       ExtractTextFromNode(node, ".//span[contains(@class,'username')]") ??
                       ExtractTextFromNode(node, ".//h2");

        if (string.IsNullOrWhiteSpace(username))
            return null;

        return new InstagramProfileModel
        {
            Username = username,
            FullName = ExtractTextFromNode(node, ".//h1") ?? ExtractTextFromNode(node, ".//div[contains(@class,'full_name')]"),
            Biography = ExtractTextFromNode(node, ".//div[contains(@class,'biography')]"),
            FollowerCount = ExtractCountFromNode(node, "followers"),
            FollowingCount = ExtractCountFromNode(node, "following"),
            PostCount = ExtractCountFromNode(node, "posts"),
            IsPrivate = node.SelectSingleNode(".//*[contains(text(),'private') or contains(text(),'Private')]") is not null
        };
    }

    private static string? ExtractUsername(HtmlDocument doc)
    {
        // Try common Instagram export username patterns
        var node = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'username')]/text()") ??
                   doc.DocumentNode.SelectSingleNode("//span[contains(@class,'username')]/text()") ??
                   doc.DocumentNode.SelectSingleNode("//h2[contains(@class,'username')]/text()") ??
                   doc.DocumentNode.SelectSingleNode("//title/text()");

        return node?.InnerText?.Trim();
    }

    private static string? ExtractText(HtmlDocument doc, string xpath)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return node?.InnerText?.Trim();
    }

    private static string? ExtractTextFromNode(HtmlNode parent, string xpath)
    {
        var node = parent.SelectSingleNode(xpath);
        return node?.InnerText?.Trim();
    }

    private static string? ExtractAttribute(HtmlDocument doc, string xpath, string attribute)
    {
        var node = doc.DocumentNode.SelectSingleNode(xpath);
        return node?.GetAttributeValue(attribute, null);
    }

    private static int ExtractCount(HtmlDocument doc, string keyword)
    {
        var nodes = doc.DocumentNode.SelectNodes($"//*[contains(text(),'{keyword}')]");
        if (nodes is null || nodes.Count == 0)
            return 0;

        foreach (var node in nodes)
        {
            var text = node.InnerText;
            var countStr = new string(text.Where(c => char.IsDigit(c)).ToArray());
            if (int.TryParse(countStr, out var count))
                return count;
        }

        return 0;
    }

    private static int ExtractCountFromNode(HtmlNode parent, string keyword)
    {
        var nodes = parent.SelectNodes($".//*[contains(text(),'{keyword}')]");
        if (nodes is null || nodes.Count == 0)
            return 0;

        foreach (var node in nodes)
        {
            var text = node.InnerText;
            var countStr = new string(text.Where(c => char.IsDigit(c)).ToArray());
            if (int.TryParse(countStr, out var count))
                return count;
        }

        return 0;
    }

    /// <summary>
    /// Detects the relationship type represented by an Instagram export HTML filename.
    /// Returns null if the file does not match a known relationship list.
    /// </summary>
    private static RelationshipType? DetectRelationshipType(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

        if (name.Contains("follower") && !name.Contains("following"))
            return RelationshipType.Followers;

        if (name.Contains("following"))
            return RelationshipType.Following;

        if (name.Contains("pending") || name.Contains("pending_follow"))
            return RelationshipType.PendingRequests;

        if (name.Contains("blocked"))
            return RelationshipType.Blocked;

        if (name.Contains("recent_follow_request"))
            return RelationshipType.RecentFollowRequests;

        if (name.Contains("removed_suggestion") || name.Contains("removed"))
            return RelationshipType.RemovedSuggestions;

        return null;
    }

    /// <summary>
    /// Parses a list of Instagram account relationships from an HTML document.
    /// Looks for anchor links that reference Instagram profile URLs and extracts the username.
    /// </summary>
    private List<InstagramRelationshipModel> ParseRelationships(HtmlDocument doc, string sourceFileName, RelationshipType type, List<string> errors)
    {
        var relationships = new List<InstagramRelationshipModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nodes = doc.DocumentNode.SelectNodes("//a[@href]") ??
                    doc.DocumentNode.SelectNodes("//a");

        if (nodes is null || nodes.Count == 0)
        {
            errors.Add($"No relationship links found in {sourceFileName}");
            return relationships;
        }

        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(href))
                continue;

            // Instagram profile URLs: https://www.instagram.com/username/ or /username/
            var match = InstagramProfileUrlRegex().Match(href);
            if (!match.Success)
                continue;

            var username = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(username))
                continue;

            // Instagram usernames are at most 30 characters and cannot contain certain characters.
            if (username.Length > 30 || username.Contains("..."))
                continue;

            if (!seen.Add(username))
                continue;

            var fullName = node.InnerText?.Trim();
            if (string.Equals(fullName, username, StringComparison.OrdinalIgnoreCase))
                fullName = null;

            relationships.Add(new InstagramRelationshipModel
            {
                Username = username,
                FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                ProfileUrl = href,
                RelationshipType = type
            });
        }

        return relationships;
    }

    [GeneratedRegex("(?:https?://(?:www\\.)?instagram\\.com)?/([^/\\?\"'\\s<>]+)/?")]
    private static partial Regex InstagramProfileUrlRegex();

    private static ImportResult BuildResult(
        bool success,
        int totalRecords,
        int importedRecords,
        int skippedRecords,
        List<string> errors,
        List<InstagramProfileModel> profiles,
        List<InstagramRelationshipModel> relationships)
    {
        return new ImportResult
        {
            Success = success,
            TotalRecords = totalRecords,
            ImportedRecords = importedRecords,
            SkippedRecords = skippedRecords,
            Errors = errors.AsReadOnly(),
            Profiles = profiles.AsReadOnly(),
            Relationships = relationships.AsReadOnly(),
            Message = success ? "HTML parsing completed successfully." : "HTML parsing completed with errors."
        };
    }
}
