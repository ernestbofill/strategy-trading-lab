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

    /// <summary>
    /// The values the app uses, all from one read. Code that sends a secret (the email sender) must take the secret and
    /// the settings it is bound to from the same call, so they can't come from two moments with a change in between.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string?>> ValuesAsync(CancellationToken cancellationToken)
    {
        var rows = await Rows(cancellationToken);
        return SettingDefinitions.All.ToDictionary(d => d.Key, d => Resolve(d, rows).Value);
    }

    /// <summary>One plain setting's value, read alone: no secret is unsealed (Sign-up asks for SIGN_UPS on every load).</summary>
    public async Task<string?> ValueAsync(SettingDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.Secret) throw new ArgumentException("Read secrets with ValuesAsync, with the settings they are bound to.", nameof(definition));
        var rows = await db.AppSettings.AsNoTracking().Where(row => row.Key == definition.Key).ToDictionaryAsync(row => row.Key, cancellationToken);
        return Resolve(definition, rows).Value;
    }

    /// <summary>
    /// Saves the page's value. A secret is sealed with the values of the settings it is bound to, and only if they are
    /// still the ones the page showed (<paramref name="expected"/>): false when they changed meanwhile, and nothing is saved.
    /// </summary>
    public async Task<bool> SetAsync(SettingDefinition definition, string value, IReadOnlyDictionary<string, string?>? expected,
        CancellationToken cancellationToken)
    {
        var rows = await Rows(cancellationToken);
        if (definition.Secret)
        {
            var bound = definition.BoundTo ?? [];
            if (expected is null || bound.Any(key => expected.GetValueOrDefault(key) != Resolve(SettingDefinitions.Find(key)!, rows).Value))
                return false;
        }
        var stored = definition.Secret ? protector.Protect(Binding(definition, rows) + Separator + value) : value;
        await Upsert(definition.Key, stored, cancellationToken);
        return true;
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
