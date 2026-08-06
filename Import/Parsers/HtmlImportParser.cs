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

        if (name.Contains("pending") || name.Contains("pending_follow"))
            return RelationshipType.PendingRequests;

        if (name.Contains("recent_follow_request"))
            return RelationshipType.RecentFollowRequests;

        if (name.Contains("follower") && !name.Contains("following"))
            return RelationshipType.Followers;

        if (name.Contains("following") && !name.Contains("hashtag"))
            return RelationshipType.Following;

        if (name.Contains("blocked"))
            return RelationshipType.Blocked;

        if (name.Contains("removed_suggestion") || name.Contains("removed"))
            return RelationshipType.RemovedSuggestions;

        if (name.Contains("unfollowed"))
            return RelationshipType.RemovedSuggestions;

        if (name.Contains("favorited") || name.Contains("favorite"))
            return RelationshipType.RemovedSuggestions;

        return null;
    }

    /// <summary>
    /// Parses a list of Instagram account relationships from an HTML document.
    /// Supports two HTML structures used by Instagram exports:
    /// 1. Link-based: &lt;a href="https://www.instagram.com/username"&gt; — used by followers/following lists.
    /// 2. Table-based: &lt;table&gt;&lt;tr&gt;&lt;td&gt;Username&lt;/td&gt;&lt;td&gt;value&lt;/td&gt;&lt;/tr&gt;&lt;/table&gt; — used by pending follow requests.
    /// </summary>
    private List<InstagramRelationshipModel> ParseRelationships(HtmlDocument doc, string sourceFileName, RelationshipType type, List<string> errors)
    {
        var relationships = new List<InstagramRelationshipModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (linkNodes is not null && linkNodes.Count > 0)
        {
            foreach (var node in linkNodes)
            {
                var href = node.GetAttributeValue("href", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(href))
                    continue;

                // Instagram profile URLs: https://www.instagram.com/username/ or /_u/username/
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
                if (string.Equals(fullName, username, StringComparison.OrdinalIgnoreCase) || string.Equals(fullName, href, StringComparison.OrdinalIgnoreCase))
                    fullName = null;

                relationships.Add(new InstagramRelationshipModel
                {
                    Username = username,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                    ProfileUrl = href,
                    RelationshipType = type
                });
            }
        }

        // If no link-based entries were found, try table-based parsing (e.g. pending_follow_requests)
        if (relationships.Count == 0)
        {
            var tableEntries = ParseTableBasedRelationships(doc, seen, type);
            if (tableEntries.Count > 0)
                relationships.AddRange(tableEntries);
        }

        if (relationships.Count == 0)
            errors.Add($"No relationship entries found in {sourceFileName}");

        return relationships;
    }

    /// <summary>
    /// Parses table-based relationship entries where each entry is a &lt;table&gt; with
    /// rows containing "Name" and "Username" label/value pairs.
    /// This structure is used by Instagram export files like pending_follow_requests.html.
    /// </summary>
    private List<InstagramRelationshipModel> ParseTableBasedRelationships(
        HtmlDocument doc,
        HashSet<string> seen,
        RelationshipType type)
    {
        var relationships = new List<InstagramRelationshipModel>();
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables is null || tables.Count == 0)
            return relationships;

        foreach (var table in tables)
        {
            string? username = null;
            string? fullName = null;
            DateTime? followDate = null;

            var rows = table.SelectNodes(".//tr");
            if (rows is null)
                continue;

            foreach (var row in rows)
            {
                var cells = row.SelectNodes(".//td");
                if (cells is null || cells.Count < 2)
                    continue;

                var label = cells[0].InnerText?.Trim().ToLowerInvariant();
                var value = cells[1].InnerText?.Trim();

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (label == "username")
                    username = value;
                else if (label == "name")
                    fullName = value;
                else if (label is "date" or "follow date" or "requested date" or "following date" or "timestamp")
                    followDate = ParseInstagramDate(value);
            }

            if (string.IsNullOrWhiteSpace(username))
                continue;

            if (username.Length > 30 || username.Contains("..."))
                continue;

            if (!seen.Add(username))
                continue;

            if (string.Equals(fullName, username, StringComparison.OrdinalIgnoreCase))
                fullName = null;

            relationships.Add(new InstagramRelationshipModel
            {
                Username = username,
                FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                ProfileUrl = $"https://www.instagram.com/{username}/",
                RelationshipType = type,
                FollowDate = followDate
            });
        }

        return relationships;
    }

    /// <summary>
    /// Parses Instagram export date formats like "Mar 23, 2016 7:06 am".
    /// </summary>
    private static DateTime? ParseInstagramDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var formats = new[]
        {
            "MMM d, yyyy h:mm tt",
            "MMM d, yyyy hh:mm tt",
            "MMM dd, yyyy h:mm tt",
            "MMM dd, yyyy hh:mm tt",
            "MMMM d, yyyy h:mm tt",
            "MMMM d, yyyy hh:mm tt",
            "MMMM dd, yyyy h:mm tt",
            "MMMM dd, yyyy hh:mm tt",
            "MMM d, yyyy",
            "MMM dd, yyyy",
            "MMMM d, yyyy",
            "MMMM dd, yyyy"
        };

        if (DateTime.TryParseExact(value.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowInnerWhite | System.Globalization.DateTimeStyles.AssumeUniversal, out var result))
            return result.ToUniversalTime();

        if (DateTime.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToUniversalTime();

        return null;
    }

    [GeneratedRegex("(?:https?://(?:www\\.)?instagram\\.com)?/(?:_u/)?([^/\\?\"'\\s<>]+)/?")]
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
