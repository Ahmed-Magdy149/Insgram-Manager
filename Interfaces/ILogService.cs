namespace Insgram_Manager.Interfaces;

/// <summary>
/// Centralized logging abstraction.
/// All application components should use this interface instead of
/// injecting ILogger directly, ensuring consistent log enrichment
/// and Serilog pipeline usage.
/// </summary>
public interface ILogService
{
    void LogInformation(string message, params object[] args);

    void LogWarning(string message, params object[] args);

    void LogError(string message, Exception? exception = null, params object[] args);

    void LogDebug(string message, params object[] args);

    void LogCritical(string message, Exception? exception = null, params object[] args);
}
