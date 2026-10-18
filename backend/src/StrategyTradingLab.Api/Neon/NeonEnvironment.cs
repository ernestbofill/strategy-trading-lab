using Npgsql;

namespace StrategyTradingLab.Api.Neon;

/// <summary>
/// Reads the variables <c>neon deploy</c> / <c>neon env pull</c> write: DATABASE_URL (pooled),
/// DATABASE_URL_UNPOOLED (direct, for migrations) and the AWS_* object storage ones.
/// </summary>
public static class NeonEnvironment
{
    /// <summary>
    /// Loads <c>.env.local</c> (or <c>.env</c>) from the nearest folder that has one, up from
    /// <paramref name="startDirectory"/>, into the process environment. Variables already set win.
    /// Local development only; deployed hosts inject the variables.
    /// </summary>
    public static string? LoadDotEnv(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var file = new[] { ".env.local", ".env" }
                .Select(name => Path.Combine(directory.FullName, name))
                .FirstOrDefault(File.Exists);
            if (file is null) continue;

            foreach (var line in File.ReadAllLines(file))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
                var separator = trimmed.IndexOf('=');
                if (separator <= 0) continue;
                var key = trimmed[..separator].Trim();
                var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
                if (Environment.GetEnvironmentVariable(key) is null) Environment.SetEnvironmentVariable(key, value);
            }
            return file;
        }
        return null;
    }

    /// <summary>Turns a <c>postgresql://user:password@host/db?sslmode=require</c> URL into an Npgsql connection string.</summary>
    public static string ToNpgsqlConnectionString(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Require,
        };

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (query["channel_binding"] == "require") builder.ChannelBinding = ChannelBinding.Require;

        // The -pooler host is PgBouncer in transaction mode: skip the session reset it doesn't need.
        if (uri.Host.Contains("-pooler")) builder.NoResetOnClose = true;

        return builder.ConnectionString;
    }

    /// <summary>
    /// The app's connection string: ConnectionStrings:Default if set, else the pooled DATABASE_URL,
    /// or the direct DATABASE_URL_UNPOOLED when <paramref name="direct"/> (migrations).
    /// </summary>
    public static string? ConnectionString(IConfiguration configuration, bool direct = false)
    {
        var configured = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        var url = direct
            ? configuration["DATABASE_URL_UNPOOLED"] ?? configuration["DATABASE_URL"]
            : configuration["DATABASE_URL"];
        return string.IsNullOrWhiteSpace(url) ? null : ToNpgsqlConnectionString(url);
    }
}
