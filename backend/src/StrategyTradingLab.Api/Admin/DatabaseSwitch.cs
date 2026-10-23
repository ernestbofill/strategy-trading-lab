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

/// <summary>
/// Which database the API uses (dev or production) and the URLs set for them on the admin page. They can't live in
/// the database they choose, so they are kept in a file in the state directory, encrypted with Data Protection like
/// the admin page's secrets. The host's own database (production on the server, dev locally) falls back to the
/// environment (DATABASE_URL, ConnectionStrings:Default); the other one is used only once a URL is set on the page.
/// A change is tested first: the database must answer and have every migration (the deploy migrates production only).
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
            var url = current.Urls.GetValueOrDefault(name);
            var source = url is not null ? DatabaseUrlSource.Page
                : name == Home && ServerConnectionString() is not null ? DatabaseUrlSource.Server
                : DatabaseUrlSource.None;
            var connection = ConnectionString(current, name);
            return new DatabaseInfo(name, source, connection is null ? null : new NpgsqlConnectionStringBuilder(connection).Host);
        })];
    }

    /// <summary>Makes <paramref name="name"/> the active database, once it answers and has every migration.</summary>
    public Task SwitchAsync(DatabaseName name, CancellationToken cancellationToken) =>
        ChangeAsync(current => current with { Active = name }, cancellationToken);

    /// <summary>Sets a database's URL on the page. Throws <see cref="DatabaseChangeException"/> for a refused value.</summary>
    public Task SetUrlAsync(DatabaseName name, string url, CancellationToken cancellationToken)
    {
        if (CheckUrl(url) is { } error) throw new DatabaseChangeException(error);
        return ChangeAsync(current => current with { Urls = new Dictionary<DatabaseName, string>(current.Urls) { [name] = url } },
            cancellationToken);
    }

    /// <summary>Forgets the page's URL: the home database goes back to the server's, the other one to none.</summary>
    public Task ResetUrlAsync(DatabaseName name, CancellationToken cancellationToken) =>
        ChangeAsync(current =>
        {
            var urls = new Dictionary<DatabaseName, string>(current.Urls);
            urls.Remove(name);
            return current with { Urls = urls };
        }, cancellationToken);

    /// <summary>A Neon URL as `neon env pull` writes it: postgresql://user:password@host/db, on a *.neon.tech host.</summary>
    public static string? CheckUrl(string url)
    {
        const string example = "Use the Neon branch's DATABASE_URL: postgresql://user:password@ep-….neon.tech/neondb?sslmode=require.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgresql" or "postgres")
            || uri.AbsolutePath.Length <= 1 || !uri.UserInfo.Contains(':'))
            return example;
        // Only Neon: whoever knows the admin password can't point the API at a server of their own elsewhere.
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
            if (connection is null) throw new DatabaseChangeException($"The {Words(after.Active)} database has no URL: set one first.");
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
            throw new DatabaseChangeException($"The {Words(name)} database is missing {pending.Count()} migration(s): migrate it first.");
    }

    string? ConnectionString(Choice from, DatabaseName name) =>
        from.Urls.GetValueOrDefault(name) is { } url ? NeonEnvironment.ToNpgsqlConnectionString(url)
        : name == Home ? ServerConnectionString()
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
            var saved = JsonSerializer.Deserialize<Choice>(protector.Unprotect(File.ReadAllText(path)))!;
            // The active one lost its URL (a new api.env): back to the home database, keeping the URLs set on the page.
            return ConnectionString(saved, saved.Active) is null ? saved with { Active = Home } : saved;
        }
        catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or JsonException or IOException
            or UriFormatException)
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
