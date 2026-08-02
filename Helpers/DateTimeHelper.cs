namespace Insgram_Manager.Helpers;

/// <summary>
/// Utility helper for date/time formatting and conversions.
/// </summary>
public static class DateTimeHelper
{
    public static string FormatRelative(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;

        return span switch
        {
            { TotalDays: > 1 } => $"{(int)span.TotalDays} days ago",
            { TotalHours: > 1 } => $"{(int)span.TotalHours} hours ago",
            { TotalMinutes: > 1 } => $"{(int)span.TotalMinutes} minutes ago",
            _ => "just now"
        };
    }

    public static string FormatShortDate(DateTime dateTime) =>
        dateTime.ToString("MMM dd, yyyy");
}
