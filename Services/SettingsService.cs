using System.Text.Json;
using Insgram_Manager.Common.Results;
using Insgram_Manager.Data;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Services;

/// <summary>
/// Settings service that reads and writes key-value settings
/// from the SQLite database via the Setting entity.
/// Uses IDbContextFactory for Blazor Server compatibility.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Result<string?>> GetAsync(string key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.Settings.FirstOrDefaultAsync(s => s.Key == key);

        return setting is not null
            ? Result<string?>.Ok(setting.Value)
            : Result.Fail<string?>($"Setting '{key}' not found.");
    }

    public async Task<Result> SetAsync(string key, string value)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.Settings.FirstOrDefaultAsync(s => s.Key == key);

        if (setting is null)
        {
            context.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            context.Settings.Update(setting);
        }

        await context.SaveChangesAsync();
        return Result.Ok($"Setting '{key}' saved.");
    }

    public async Task<Result<T?>> GetAsync<T>(string key)
    {
        var rawResult = await GetAsync(key);

        if (!rawResult.Success || rawResult.Data is null)
            return Result.Fail<T?>($"Setting '{key}' not found or null.");

        try
        {
            var value = JsonSerializer.Deserialize<T>(rawResult.Data, JsonOptions);
            return Result<T?>.Ok(value);
        }
        catch (JsonException ex)
        {
            return Result.Fail<T?>($"Failed to deserialize setting '{key}': {ex.Message}");
        }
    }

    public async Task<Result> SetAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return await SetAsync(key, json);
    }

    public async Task<Result> DeleteAsync(string key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.Settings.FirstOrDefaultAsync(s => s.Key == key);

        if (setting is null)
            return Result.Fail($"Setting '{key}' not found.");

        context.Settings.Remove(setting);
        await context.SaveChangesAsync();
        return Result.Ok($"Setting '{key}' deleted.");
    }
}
