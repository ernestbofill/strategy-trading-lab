using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Admin;

/// <summary>The two Neon branches the API can use.</summary>
public enum DatabaseName { Dev, Production }

/// <summary>
/// What the admin page shows for a database: the server variable its URL comes from, and the URL's host (null when
/// the variable isn't set). The URL holds a password, so only the host is sent.
/// </summary>
public record DatabaseInfo(DatabaseName Name, string Variable, string? Host);

/// <summary>Why a switch was refused, in words for the admin page. Never holds a password.</summary>
public class DatabaseChangeException(string message) : Exception(message);

/// <summary>The database answers but lacks some of this build's migrations.</summary>
public class MissingMigrationsException(string message) : DatabaseChangeException(message);

/// <summary>
/// Which database the API uses: dev or production. Both URLs come only from the server's environment (Ernest's call,
/// #30), so the admin page can switch between them but never point the API anywhere else: the host's own database
/// (production on the server, dev locally) from DATABASE_URL or ConnectionStrings:Default, the one the deploy and
/// local startup migrate, and the other one from DATABASE_URL_DEV or DATABASE_URL_PRODUCTION. The choice can't live in
/// the database it chooses, so it is kept in database.json in the state directory. A switch is tested first: the
/// database must answer and have every migration.
/// </summary>
public class DatabaseSwitch
{
    const string FileName = "database.json";

    readonly IConfiguration configuration;
    readonly ILogger<DatabaseSwitch> logger;
    readonly string path;
    readonly Lock writing = new();
    volatile Choice choice;

    public DatabaseSwitch(IConfiguration configuration, IHostEnvironment environment, ILogger<DatabaseSwitch> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
        Home = environment.IsDevelopment() ? DatabaseName.Dev : DatabaseName.Production;
        path = Path.Combine(StateDirectory(configuration), FileName);
        choice = Load();
    }

    /// <summary>This host's own database, the one its environment's DATABASE_URL points at.</summary>
    public DatabaseName Home { get; }

    public DatabaseName Active => choice.Active;

    /// <summary>The connection string of the active database: read for every new context.</summary>
    public string? ConnectionString() => ConnectionString(choice.Active);

    public IReadOnlyList<DatabaseInfo> Databases() =>
        [.. Enum.GetValues<DatabaseName>().Select(name => new DatabaseInfo(name, Variable(name),
            ConnectionString(name) is { } connection ? new NpgsqlConnectionStringBuilder(connection).Host : null))];

    /// <summary>
    /// At startup, in the background: the other database must still have every migration (a deploy migrates production
    /// only, so a release can leave dev behind). If it lacks some, the API uses its home database until the next start,
    /// which checks again; the saved choice stays. One that doesn't answer is left as it is: it may be a blip, and the
    /// admin page can switch back.
    /// </summary>
    public async Task CheckActiveAsync(CancellationToken cancellationToken)
    {
        var current = choice;
        if (current.Active == Home || ConnectionString(current.Active) is not { } connection) return;
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

    /// <summary>
    /// Makes <paramref name="name"/> the active database, once it answers and has every migration, and saves the choice.
    /// One switch at a time, so two can't overwrite each other.
    /// </summary>
    public async Task SwitchAsync(DatabaseName name, CancellationToken cancellationToken)
    {
        // Lock can't be held across an await: test outside it, then apply only if nothing changed meanwhile.
        var before = choice;
        if (name == before.Active) return;
        if (ConnectionString(name) is not { } connection)
            throw new DatabaseChangeException($"The {Words(name)} database has no URL: set {Variable(name)} on the server.");
        await CheckAsync(name, connection, cancellationToken);

        var after = new Choice(name);
        lock (writing)
        {
            if (!ReferenceEquals(choice, before)) throw new DatabaseChangeException("The database changed meanwhile. Reload and try again.");
            Save(after);
            choice = after;
        }
        logger.LogWarning("Switched on the admin page: the API now uses the {Database} database ({Host}).",
            Words(name), new NpgsqlConnectionStringBuilder(connection).Host);
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

    /// <summary>The variable a database's URL comes from: DATABASE_URL for the home one, else DATABASE_URL_DEV or _PRODUCTION.</summary>
    string Variable(DatabaseName name) => name == Home ? "DATABASE_URL" : $"DATABASE_URL_{name.ToString().ToUpperInvariant()}";

    string? ConnectionString(DatabaseName name)
    {
        if (name == Home) return NeonEnvironment.ConnectionString(configuration);
        return configuration[Variable(name)] is { Length: > 0 } url ? NeonEnvironment.ToNpgsqlConnectionString(url) : null;
    }

    static string Words(DatabaseName name) => name == DatabaseName.Dev ? "dev" : "production";

    /// <summary>The service's StateDirectory on the server; locally, a per-user folder.</summary>
    static string StateDirectory(IConfiguration configuration) =>
        configuration["STATE_DIRECTORY"] is { Length: > 0 } state
            ? state
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StrategyTradingLab");

    Choice Load()
    {
        var home = new Choice(Home);
        if (!File.Exists(path)) return home;
        try
        {
            if (JsonSerializer.Deserialize<Choice>(File.ReadAllText(path)) is not { } saved || !Enum.IsDefined(saved.Active))
                throw new JsonException("Not a database choice.");
            // Its URL is gone from the environment: back to the home database.
            return ConnectionString(saved.Active) is null ? home : saved;
        }
        catch (Exception error) when (error is JsonException or IOException or FormatException or ArgumentException)
        {
            // Safe side: the host's own database, as if nothing had been chosen.
            logger.LogWarning(error, "{Path} can't be read: using the {Database} database.", path, Words(Home));
            return home;
        }
    }

    void Save(Choice value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Written next to it, then moved over it, so a crash never leaves half a file.
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value));
        File.Move(temporary, path, overwrite: true);
    }

    record Choice(DatabaseName Active);
}
