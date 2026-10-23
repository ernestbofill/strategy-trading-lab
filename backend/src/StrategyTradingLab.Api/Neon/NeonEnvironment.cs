using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Npgsql;

namespace StrategyTradingLab.Api.Neon;

/// <summary>
/// Reads the variables <c>neon deploy</c> / <c>neon env pull</c> write: DATABASE_URL (pooled),
/// DATABASE_URL_UNPOOLED (direct, for migrations) and the AWS_* object storage ones.
/// </summary>
public static class NeonEnvironment
{
    /// <summary>
    /// Adds <c>.env.local</c> (or <c>.env</c>) from the nearest folder that has one, up from
    /// <paramref name="startDirectory"/>, as a configuration source. Local development only;
    /// deployed hosts set the variables in the environment.
    /// </summary>
    public static IConfigurationBuilder AddNeonDotEnv(this IConfigurationBuilder configuration, string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var file = new[] { ".env.local", ".env" }
                .FirstOrDefault(name => File.Exists(Path.Combine(directory.FullName, name)));
            if (file is null) continue;

            // KEY=value lines with # comments are what the INI provider reads. The default file
            // provider hides dot-files, so this one excludes nothing.
            var files = new PhysicalFileProvider(directory.FullName, ExclusionFilters.None);
            return configuration.AddIniFile(files, file, optional: false, reloadOnChange: false);
        }
        return configuration;
    }

    /// <summary>
    /// Turns a <c>postgresql://user:password@host/db?sslmode=require</c> URL into an Npgsql connection string. SSL is
    /// required unless the URL says <c>sslmode=disable</c> (CI's local PostgreSQL); the URLs come only from the
    /// server's environment.
    /// </summary>
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
        if (query["sslmode"] == "disable") builder.SslMode = SslMode.Disable;
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
