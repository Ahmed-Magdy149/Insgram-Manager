using Insgram_Manager.Common.Results;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for reading and writing application settings
/// persisted in the database via the Setting entity.
/// </summary>
public interface ISettingsService
{
    Task<Result<string?>> GetAsync(string key);

    Task<Result> SetAsync(string key, string value);

    Task<Result<T?>> GetAsync<T>(string key);

    Task<Result> SetAsync<T>(string key, T value);

    Task<Result> DeleteAsync(string key);
}
