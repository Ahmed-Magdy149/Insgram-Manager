using Insgram_Manager.Interfaces;
using Serilog;

namespace Insgram_Manager.Services;

/// <summary>
/// Centralized logging service that wraps Serilog's static logger.
/// All application components should use ILogService instead of
/// injecting ILogger directly to ensure consistent log enrichment.
/// </summary>
public sealed class LogService : ILogService
{
    public void LogInformation(string message, params object[] args) =>
        Log.Information(message, args);

    public void LogWarning(string message, params object[] args) =>
        Log.Warning(message, args);

    public void LogError(string message, Exception? exception = null, params object[] args)
    {
        if (exception is not null)
            Log.Error(exception, message, args);
        else
            Log.Error(message, args);
    }

    public void LogDebug(string message, params object[] args) =>
        Log.Debug(message, args);

    public void LogCritical(string message, Exception? exception = null, params object[] args)
    {
        if (exception is not null)
            Log.Fatal(exception, message, args);
        else
            Log.Fatal(message, args);
    }
}
