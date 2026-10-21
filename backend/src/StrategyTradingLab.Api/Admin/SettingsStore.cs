using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Admin;

/// <summary>Where a setting's value comes from: the admin page, the server's environment, the default, or nowhere.</summary>
public enum SettingSource { Page, Server, Default, None }

public record EffectiveSetting(SettingDefinition Definition, string? Value, SettingSource Source);

/// <summary>Reads and writes the admin page's settings. Secrets are encrypted with ASP.NET Core Data Protection.</summary>
public class SettingsStore(AppDbContext db, IConfiguration configuration, IDataProtectionProvider dataProtection)
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

    /// <summary>Saves the page's value. When it changes, the settings it guards are cleared.</summary>
    public async Task SetAsync(SettingDefinition definition, string value, CancellationToken cancellationToken)
    {
        var before = await GetAsync(definition.Key, cancellationToken);
        await Upsert(definition.Key, definition.Secret ? protector.Protect(value) : value, cancellationToken);
        if (before != value)
        {
            foreach (var guarded in definition.ClearsOnChange ?? []) await Upsert(guarded, null, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Forgets the page's value, so the server's value or the default applies again.</summary>
    public Task ResetAsync(string key, CancellationToken cancellationToken) =>
        db.AppSettings.Where(row => row.Key == key).ExecuteDeleteAsync(cancellationToken);

    /// <summary>Saves an empty value that hides the server's too (a secret the page cleared).</summary>
    public async Task ClearAsync(string key, CancellationToken cancellationToken)
    {
        await Upsert(key, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    async Task Upsert(string key, string? stored, CancellationToken cancellationToken)
    {
        var row = await db.AppSettings.FindAsync([key], cancellationToken);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = key, Value = stored });
        else row.Value = stored;
    }

    EffectiveSetting Resolve(SettingDefinition definition, AppSetting? row)
    {
        if (row is not null)
        {
            return row.Value is null
                ? new(definition, null, SettingSource.None)
                : new(definition, definition.Secret ? Unprotect(row.Value) : row.Value, SettingSource.Page);
        }
        if (configuration[definition.Key] is { Length: > 0 } server) return new(definition, server, SettingSource.Server);
        return definition.Default is null ? new(definition, null, SettingSource.None) : new(definition, definition.Default, SettingSource.Default);
    }

    /// <summary>Null when the key that encrypted it is gone (a new server): the secret has to be set again.</summary>
    string? Unprotect(string stored)
    {
        try { return protector.Unprotect(stored); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }
}
