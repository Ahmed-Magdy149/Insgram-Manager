using Insgram_Manager.Interfaces;

namespace Insgram_Manager.Services;

/// <summary>
/// Base class for all application services providing centralized access
/// to logging and settings. Derived services inherit common dependencies
/// without repeating injection boilerplate.
/// </summary>
public abstract class BaseService
{
    protected ILogService Logger { get; }

    protected ISettingsService Settings { get; }

    protected BaseService(ILogService logger, ISettingsService settings)
    {
        Logger = logger;
        Settings = settings;
    }
}
