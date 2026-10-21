using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Admin;

/// <summary>Where a setting's value comes from: the admin page, the server's environment, the default, or nowhere.</summary>
public enum SettingSource { Page, Server, Default, None }

public record EffectiveSetting(SettingDefinition Definition, string? Value, SettingSource Source);

/// <summary>
/// Reads and writes the admin page's settings. A secret comes only from the page, never from the server's environment,
/// and is encrypted (ASP.NET Core Data Protection) together with the values of the settings it is bound to when it was
/// saved. It only counts as set while those still have the same values, so whatever changes them (a save, Default, a
/// new api.env, lost keys) can't send it somewhere new: the check is made on every read.
/// </summary>
public class SettingsStore(AppDbContext db, IConfiguration configuration, IDataProtectionProvider dataProtection,
    ILogger<SettingsStore> logger)
{
    readonly IDataProtector protector = dataProtection.CreateProtector("StrategyTradingLab.Admin.Settings");

    public async Task<IReadOnlyList<EffectiveSetting>> AllAsync(CancellationToken cancellationToken)
    {
        var rows = await Rows(cancellationToken);
        return [.. SettingDefinitions.All.Select(definition => Resolve(definition, rows))];
    }

    /// <summary>The value the app uses, or null when the setting has none.</summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var definition = SettingDefinitions.Find(key) ?? throw new ArgumentException($"Unknown setting {key}.", nameof(key));
        return Resolve(definition, await Rows(cancellationToken)).Value;
    }

    /// <summary>Saves the page's value. A secret is sealed with the current values of the settings it is bound to.</summary>
    public async Task SetAsync(SettingDefinition definition, string value, CancellationToken cancellationToken)
    {
        var rows = await Rows(cancellationToken);
        var stored = definition.Secret ? protector.Protect(Binding(definition, rows) + Separator + value) : value;
        await Upsert(definition.Key, stored, cancellationToken);
    }

    /// <summary>Forgets the page's value, so the server's value or the default applies again (a secret has neither).</summary>
    public Task ResetAsync(string key, CancellationToken cancellationToken) =>
        db.AppSettings.Where(row => row.Key == key).ExecuteDeleteAsync(cancellationToken);

    /// <summary>No value at all: a secret is forgotten; any other setting also hides the server's value.</summary>
    public Task ClearAsync(SettingDefinition definition, CancellationToken cancellationToken) =>
        definition.Secret ? ResetAsync(definition.Key, cancellationToken) : Upsert(definition.Key, null, cancellationToken);

    const char Separator = '\n';

    Task<Dictionary<string, AppSetting>> Rows(CancellationToken cancellationToken) =>
        db.AppSettings.AsNoTracking().ToDictionaryAsync(row => row.Key, cancellationToken);

    async Task Upsert(string key, string? stored, CancellationToken cancellationToken)
    {
        var row = await db.AppSettings.FindAsync([key], cancellationToken);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = key, Value = stored });
        else row.Value = stored;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The bound settings' current values, as one string sealed into the secret.</summary>
    string Binding(SettingDefinition secret, Dictionary<string, AppSetting> rows) =>
        string.Join('\u001f', (secret.BoundTo ?? []).Select(key => Resolve(SettingDefinitions.Find(key)!, rows).Value ?? ""));

    EffectiveSetting Resolve(SettingDefinition definition, Dictionary<string, AppSetting> rows)
    {
        var row = rows.GetValueOrDefault(definition.Key);
        if (definition.Secret)
        {
            return row?.Value is { } sealedValue && Unseal(definition, sealedValue, rows) is { } secret
                ? new(definition, secret, SettingSource.Page)
                : new(definition, null, SettingSource.None);
        }
        if (row is not null) return new(definition, row.Value, row.Value is null ? SettingSource.None : SettingSource.Page);
        if (configuration[definition.Key] is { Length: > 0 } server) return new(definition, server, SettingSource.Server);
        return definition.Default is null ? new(definition, null, SettingSource.None) : new(definition, definition.Default, SettingSource.Default);
    }

    /// <summary>The secret, or null when it can't be read (the keys are gone) or its bound settings have changed since.</summary>
    string? Unseal(SettingDefinition definition, string sealedValue, Dictionary<string, AppSetting> rows)
    {
        string plain;
        try { plain = protector.Unprotect(sealedValue); }
        catch (System.Security.Cryptography.CryptographicException)
        {
            logger.LogWarning("The saved {Setting} can't be decrypted (its Data Protection key is gone). Set it again on the admin page.", definition.Key);
            return null;
        }
        var at = plain.IndexOf(Separator);
        return at >= 0 && plain[..at] == Binding(definition, rows) ? plain[(at + 1)..] : null;
    }
}
