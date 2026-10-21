using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Admin;

/// <summary>Where a setting's value comes from: the admin page, the server's environment, the default, or nowhere.</summary>
public enum SettingSource { Page, Server, Default, None }

public record EffectiveSetting(SettingDefinition Definition, string? Value, SettingSource Source);

/// <summary>Reads and writes the admin page's settings. Secrets are encrypted with ASP.NET Core Data Protection.</summary>
public class SettingsStore(AppDbContext db, IConfiguration configuration, IDataProtectionProvider dataProtection,
    ILogger<SettingsStore> logger)
{
    readonly IDataProtector protector = dataProtection.CreateProtector("StrategyTradingLab.Admin.Settings");

    public async Task<IReadOnlyList<EffectiveSetting>> AllAsync(CancellationToken cancellationToken)
    {
        var rows = await db.AppSettings.AsNoTracking().ToDictionaryAsync(row => row.Key, cancellationToken);
        return [.. SettingDefinitions.All.Select(definition => Resolve(definition, rows.GetValueOrDefault(definition.Key)))];
    }

    /// <summary>The value the app uses, or null when the setting has none.</summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var definition = SettingDefinitions.Find(key) ?? throw new ArgumentException($"Unknown setting {key}.", nameof(key));
        var row = await db.AppSettings.AsNoTracking().SingleOrDefaultAsync(row => row.Key == key, cancellationToken);
        return Resolve(definition, row).Value;
    }

    /// <summary>Saves the page's value.</summary>
    public Task SetAsync(SettingDefinition definition, string value, CancellationToken cancellationToken) =>
        Change(rows => Store(rows, definition.Key, definition.Secret ? protector.Protect(value) : value), cancellationToken);

    /// <summary>Forgets the page's value, so the server's value or the default applies again.</summary>
    public Task ResetAsync(string key, CancellationToken cancellationToken) =>
        Change(rows =>
        {
            if (rows.Remove(key, out var row)) db.AppSettings.Remove(row);
        }, cancellationToken);

    /// <summary>Saves an empty value that hides the server's too (a secret the page cleared).</summary>
    public Task ClearAsync(string key, CancellationToken cancellationToken) =>
        Change(rows => Store(rows, key, null), cancellationToken);

    /// <summary>
    /// Every write goes through here: it applies the change, then clears each secret whose bound settings now have
    /// another value than before. The table is a handful of rows, so it is read whole, once.
    /// </summary>
    async Task Change(Action<Dictionary<string, AppSetting>> change, CancellationToken cancellationToken)
    {
        var rows = await db.AppSettings.ToDictionaryAsync(row => row.Key, cancellationToken);
        var before = Values(rows);
        change(rows);
        var after = Values(rows);
        foreach (var secret in SettingDefinitions.All.Where(d => d.BoundTo is not null))
        {
            if (secret.BoundTo!.Any(key => before[key] != after[key])) Store(rows, secret.Key, null);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The effective value of every setting that isn't secret (bound settings never are).</summary>
    Dictionary<string, string?> Values(Dictionary<string, AppSetting> rows) =>
        SettingDefinitions.All.Where(d => !d.Secret)
            .ToDictionary(d => d.Key, d => Resolve(d, rows.GetValueOrDefault(d.Key)).Value);

    /// <summary>Sets a loaded (tracked) row's value, or adds the row.</summary>
    void Store(Dictionary<string, AppSetting> rows, string key, string? stored)
    {
        if (rows.TryGetValue(key, out var row)) row.Value = stored;
        else rows[key] = db.AppSettings.Add(new AppSetting { Key = key, Value = stored }).Entity;
    }

    EffectiveSetting Resolve(SettingDefinition definition, AppSetting? row)
    {
        if (row is not null)
        {
            if (row.Value is null) return new(definition, null, SettingSource.None);
            if (!definition.Secret) return new(definition, row.Value, SettingSource.Page);
            // A secret the keys can no longer read counts as not set here, so the server's value still applies.
            if (Unprotect(definition.Key, row.Value) is { } secret) return new(definition, secret, SettingSource.Page);
        }
        if (configuration[definition.Key] is { Length: > 0 } server) return new(definition, server, SettingSource.Server);
        return definition.Default is null ? new(definition, null, SettingSource.None) : new(definition, definition.Default, SettingSource.Default);
    }

    /// <summary>Null when the key that encrypted it is gone (a new server or lost keys): the secret has to be set again.</summary>
    string? Unprotect(string key, string stored)
    {
        try { return protector.Unprotect(stored); }
        catch (System.Security.Cryptography.CryptographicException)
        {
            logger.LogWarning("The saved {Setting} can't be decrypted (its Data Protection key is gone). Set it again on the admin page.", key);
            return null;
        }
    }
}
