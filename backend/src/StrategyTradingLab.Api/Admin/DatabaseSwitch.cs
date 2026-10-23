using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Admin;

/// <summary>The two Neon branches the API can use.</summary>
public enum DatabaseName { Dev, Production }

/// <summary>Where a database's URL comes from: the admin page, the server's environment, or nowhere.</summary>
public enum DatabaseUrlSource { Page, Server, None }

/// <summary>What the admin page shows for a database. Its URL holds a password, so only the host is sent.</summary>
public record DatabaseInfo(DatabaseName Name, DatabaseUrlSource Source, string? Host);

/// <summary>Why a database change was refused, in words for the admin page. Never holds a password.</summary>
public class DatabaseChangeException(string message) : Exception(message);

/// <summary>The database answers but lacks some of this build's migrations.</summary>
public class MissingMigrationsException(string message) : DatabaseChangeException(message);

/// <summary>
/// Which database the API uses (dev or production) and the URL set on the admin page for the other one. They can't
/// live in the database they choose, so they are kept in a file in the state directory, encrypted with Data Protection
/// like the admin page's secrets. The host's own database (production on the server, dev locally) comes only from the
/// environment (DATABASE_URL, ConnectionStrings:Default), the one the deploy and local startup migrate; the other one
/// is used only once a URL is set on the page. A change is tested first: the database must answer and have every
/// migration.
/// </summary>
public class DatabaseSwitch
{
    const string FileName = "database.json";

    readonly IConfiguration configuration;
    readonly IDataProtector protector;
    readonly ILogger<DatabaseSwitch> logger;
    readonly string path;
    readonly Lock writing = new();
    volatile Choice choice;

    public DatabaseSwitch(IConfiguration configuration, IHostEnvironment environment, IDataProtectionProvider dataProtection,
        ILogger<DatabaseSwitch> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
        protector = dataProtection.CreateProtector("StrategyTradingLab.Admin.Database");
        Home = environment.IsDevelopment() ? DatabaseName.Dev : DatabaseName.Production;
        path = Path.Combine(StateDirectory(configuration), FileName);
        choice = Load();
    }

    /// <summary>This host's own database, the one its environment's DATABASE_URL points at.</summary>
    public DatabaseName Home { get; }

    public DatabaseName Active => choice.Active;

    /// <summary>The connection string of the active database: read for every new context.</summary>
    public string? ConnectionString() => ConnectionString(choice, choice.Active);

    public IReadOnlyList<DatabaseInfo> Databases()
    {
        var current = choice;
        return [.. Enum.GetValues<DatabaseName>().Select(name =>
        {
            var connection = ConnectionString(current, name);
            var source = connection is null ? DatabaseUrlSource.None
                : name == Home ? DatabaseUrlSource.Server
                : DatabaseUrlSource.Page;
            return new DatabaseInfo(name, source, connection is null ? null : new NpgsqlConnectionStringBuilder(connection).Host);
        })];
    }

    /// <summary>
    /// At startup, in the background: the other database must still have every migration (a deploy migrates production
    /// only, so a release can leave dev behind). If it lacks some, the API uses its home database until the next start,
    /// which checks again; the saved choice stays. One that doesn't answer is left as it is: it may be a blip, and the
    /// admin page can switch back.
    /// </summary>
    public async Task CheckActiveAsync(CancellationToken cancellationToken)
    {
        var current = choice;
        if (current.Active == Home || ConnectionString(current, current.Active) is not { } connection) return;
        try
        {
            await CheckAsync(current.Active, connection, cancellationToken);
        }
        catch (MissingMigrationsException error)
        {
            logger.LogWarning("{Reason} Using the {Database} database until the next start.", error.Message, Words(Home));
            lock (writing)
            {
                if (ReferenceEquals(choice, current)) choice = current with { Active = Home };
            }
        }
        catch (DatabaseChangeException error)
        {
            logger.LogWarning("Startup check: {Reason}", error.Message);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    /// <summary>Makes <paramref name="name"/> the active database, once it answers and has every migration.</summary>
    public Task SwitchAsync(DatabaseName name, CancellationToken cancellationToken) =>
        ChangeAsync(current => current with { Active = name }, cancellationToken);

    /// <summary>Sets the other database's URL on the page. Throws <see cref="DatabaseChangeException"/> for a refused value.</summary>
    public Task SetUrlAsync(DatabaseName name, string url, CancellationToken cancellationToken)
    {
        if (name == Home) throw new DatabaseChangeException(HomeFromServer);
        if (CheckUrl(url) is { } error) throw new DatabaseChangeException(error);
        // Not the home database under the other name: it would be migrated or written to as if it were something else.
        if (ServerConnectionString() is { } server && Endpoint(new NpgsqlConnectionStringBuilder(server).Host) == Endpoint(new Uri(url).Host))
            throw new DatabaseChangeException($"That is the {Words(Home)} database: use the {Words(name)} branch's URL.");
        return ChangeAsync(current => current with { Urls = new Dictionary<DatabaseName, string>(current.Urls) { [name] = url } },
            cancellationToken);
    }

    /// <summary>Forgets the page's URL for the other database.</summary>
    public Task ResetUrlAsync(DatabaseName name, CancellationToken cancellationToken) =>
        name == Home ? throw new DatabaseChangeException(HomeFromServer) : ChangeAsync(current =>
        {
            var urls = new Dictionary<DatabaseName, string>(current.Urls);
            urls.Remove(name);
            return current with { Urls = urls };
        }, cancellationToken);

    /// <summary>A Neon host without "-pooler", so a branch's pooled and direct URLs compare equal.</summary>
    static string? Endpoint(string? host) => host?.ToLowerInvariant().Replace("-pooler.", ".");

    string HomeFromServer => $"The {Words(Home)} database's URL comes from the server's environment.";

    /// <summary>A Neon URL as `neon env pull` writes it: postgresql://user:password@host/db, on a *.neon.tech host.</summary>
    public static string? CheckUrl(string url)
    {
        const string example = "Use the Neon branch's DATABASE_URL: postgresql://user:password@ep-….neon.tech/neondb?sslmode=require.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgresql" or "postgres")
            || uri.AbsolutePath.Length <= 1 || !uri.UserInfo.Contains(':'))
            return example;
        // Only Neon hosts: no server of any other kind. It doesn't tie the URL to Ernest's project: anyone can make a
        // Neon project, so whoever knows the admin password still could (see PR #30).
        if (!uri.Host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase)) return "Use a Neon database (a host ending in .neon.tech).";
        try
        {
            NeonEnvironment.ToNpgsqlConnectionString(url);
            return null;
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            return example;
        }
    }

    /// <summary>
    /// Applies <paramref name="change"/> if the database it leaves active answers and has every migration, then saves it.
    /// One change at a time, so two can't overwrite each other.
    /// </summary>
    async Task ChangeAsync(Func<Choice, Choice> change, CancellationToken cancellationToken)
    {
        // Lock can't be held across an await: test outside it, then apply only if nothing changed meanwhile.
        var before = choice;
        var after = change(before);
        // Only a change to the database in use or its connection is checked: a URL for the other one is saved as it is.
        var connection = ConnectionString(after, after.Active);
        var previous = ConnectionString(before, before.Active);
        if (after.Active != before.Active || connection != previous)
        {
            if (connection is null)
                throw new DatabaseChangeException(after.Active == before.Active
                    ? $"The {Words(after.Active)} database is in use: switch to the other one first."
                    : $"The {Words(after.Active)} database has no URL: set one first.");
            if (connection != previous) await CheckAsync(after.Active, connection, cancellationToken);
        }

        lock (writing)
        {
            if (!ReferenceEquals(choice, before)) throw new DatabaseChangeException("The database settings changed meanwhile. Reload and try again.");
            Save(after);
            choice = after;
        }
        logger.LogWarning("Database settings changed on the admin page: the API now uses the {Database} database ({Host}).",
            Words(after.Active), connection is null ? "none" : new NpgsqlConnectionStringBuilder(connection).Host);
    }

    static async Task CheckAsync(DatabaseName name, string connection, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, new NpgsqlConnectionStringBuilder(connection) { Timeout = 10 }.ConnectionString);
        await using var db = new AppDbContext(options.Options);
        IEnumerable<string> pending;
        try
        {
            pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            // Npgsql's messages name the host and user, never the password.
            throw new DatabaseChangeException($"Can't reach the {Words(name)} database: {error.Message}");
        }
        if (pending.Any())
            throw new MissingMigrationsException($"The {Words(name)} database is missing {pending.Count()} migration(s): migrate it first.");
    }

    string? ConnectionString(Choice from, DatabaseName name) =>
        name == Home ? ServerConnectionString()
        : from.Urls.GetValueOrDefault(name) is { } url ? NeonEnvironment.ToNpgsqlConnectionString(url)
        : null;

    string? ServerConnectionString() => NeonEnvironment.ConnectionString(configuration);

    static string Words(DatabaseName name) => name == DatabaseName.Dev ? "dev" : "production";

    /// <summary>The service's StateDirectory on the server; locally, a per-user folder.</summary>
    static string StateDirectory(IConfiguration configuration) =>
        configuration["STATE_DIRECTORY"] is { Length: > 0 } state
            ? state
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StrategyTradingLab");

    Choice Load()
    {
        var home = new Choice(Home, new Dictionary<DatabaseName, string>());
        if (!File.Exists(path)) return home;
        try
        {
            if (JsonSerializer.Deserialize<Choice>(protector.Unprotect(File.ReadAllText(path))) is not { Urls: not null } saved
                || !Enum.IsDefined(saved.Active))
                throw new JsonException("Not a database choice.");
            // The active one lost its URL (a new api.env): back to the home database, keeping the URLs set on the page.
            return ConnectionString(saved, saved.Active) is null ? saved with { Active = Home } : saved;
        }
        catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or JsonException or IOException
            or FormatException or ArgumentException)
        {
            // Safe side: the host's own database, as if nothing had been set.
            logger.LogWarning(error, "{Path} can't be read (lost Data Protection keys?): using the {Database} database. Set the database again on the admin page.",
                path, Words(Home));
            return home;
        }
    }

    void Save(Choice value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Written next to it, then moved over it, so a crash never leaves half a file.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, protector.Protect(JsonSerializer.Serialize(value)));
        File.Move(temporary, path, overwrite: true);
    }

    record Choice(DatabaseName Active, IReadOnlyDictionary<DatabaseName, string> Urls);
}
