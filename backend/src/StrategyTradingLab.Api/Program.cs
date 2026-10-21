using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Neon;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

var builder = WebApplication.CreateBuilder(args);

// Locally, `neon deploy` / `neon env pull` write the Neon variables to .env.local at the repo root.
if (builder.Environment.IsDevelopment()) builder.Configuration.AddNeonDotEnv(AppContext.BaseDirectory);

// Read when a context is created, so configuration added after this point (tests) applies.
builder.Services.AddDbContext<AppDbContext>((services, options) => AppDbContextOptions.Configure(options,
    NeonEnvironment.ConnectionString(services.GetRequiredService<IConfiguration>())
    ?? throw new InvalidOperationException(
        "No database: set DATABASE_URL (run `neon env pull`) or ConnectionStrings:Default.")));

builder.Services.TryAddSingleton(TimeProvider.System);

// Keys that encrypt the admin page's secrets and its cookie. On the server, in the systemd StateDirectory (kept
// across releases, readable only by the service); elsewhere, Data Protection's default per-user folder.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("StrategyTradingLab");
if (builder.Configuration["STATE_DIRECTORY"] is { Length: > 0 } state)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(state, "keys")));
else if (builder.Environment.IsProduction())
    // Under the unit's ProtectSystem=strict the default folder can't be written: keys would live in memory only and
    // every restart would lose the saved secrets. Fail the deploy's health check instead.
    throw new InvalidOperationException("STATE_DIRECTORY isn't set: run the API from its systemd unit (StateDirectory=).");
builder.Services.AddScoped<SettingsStore>();

// nginx (on the same machine) passes the client's address and https on. Only loopback proxies are trusted
// (ForwardedHeadersOptions' default), so a client can't fake its address to dodge the log-in rate limit.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // 5 tries per 15 minutes from one address. An IPv6 client counts per /64, since one usually has the whole block.
    options.AddPolicy(AdminEndpoints.LogInRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        AdminEndpoints.ClientKey(http.Connection.RemoteIpAddress),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15) }));
});
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.Configure<ObjectStorageOptions>(builder.Configuration.GetSection("ObjectStorage"));
if (ObjectStorage.IsConfigured(builder.Configuration))
{
    builder.Services.AddSingleton(_ => ObjectStorage.CreateClient(builder.Configuration));
}

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRateLimiter();

// The launch profiles set this, so running the API locally brings the dev database up to date.
// Tests, cloud sessions (scripts/migrate-dev-https.py) and production (the deploy's migrations bundle) don't use it.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document at /openapi/v1.json, browsable at /swagger.
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Strategy Trading Lab API"));
}

// Checks each Neon service the API depends on can be reached.
// Outside Development it only says "ok": the server version and bucket aren't for anyone to read.
app.MapGet("/health", async Task<Results<Ok<HealthResponse>, ProblemHttpResult>> (AppDbContext db,
    [FromServices] IAmazonS3? s3, IOptions<ObjectStorageOptions> storage, IHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var bucket = storage.Value.Bucket;
    var postgres = db.Database.SqlQueryRaw<string>("select version() as \"Value\"").SingleAsync(cancellationToken);
    var listing = s3?.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, MaxKeys = 1 }, cancellationToken);
    try
    {
        await Task.WhenAll(listing is null ? [postgres] : [postgres, listing]);
    }
    catch (Exception error) when (error is not OperationCanceledException)
    {
        return TypedResults.Problem(environment.IsDevelopment() ? error.Message : null,
            statusCode: StatusCodes.Status503ServiceUnavailable, title: "A Neon service can't be reached.");
    }

    if (!environment.IsDevelopment())
    {
        return TypedResults.Ok(new HealthResponse("ok", listing is null ? "not configured" : "ok"));
    }
    var objectStorage = listing is null
        ? "not configured"
        : $"ok: bucket {bucket} ({listing.Result.KeyCount ?? 0} key(s) in the first page)";
    return TypedResults.Ok(new HealthResponse(postgres.Result, objectStorage));
})
.WithName("Health")
.WithSummary("Checks the API can reach Postgres and object storage on its Neon branch.");

app.MapTemplates();
app.MapStrategies();
app.MapAdmin();

// For the client to show which API it talks to.
app.MapGet("/version", () => TypedResults.Ok(VersionResponse.Current))
.WithName("Version")
.WithSummary("The API's version, the commit it was built from and when it was built (Release builds).");

app.Run();

record HealthResponse(string Postgres, string ObjectStorage);

/// <summary>
/// Version from backend/Directory.Build.props; Commit and BuiltAt are stamped into the assembly at build time.
/// BuiltAt is null in Debug builds, where stamping it would rebuild the project every time.
/// </summary>
record VersionResponse(string Version, string? Commit, DateTimeOffset? BuiltAt)
{
    public static readonly VersionResponse Current = Read(typeof(Program).Assembly);

    static VersionResponse Read(Assembly assembly)
    {
        // "0.1.0-alpha.2+<commit sha>": the SDK appends the commit when it builds from a git checkout.
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+', 2);
        var builtAt = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(m => m.Key == "BuiltAt")?.Value;
        return new(informational[0], informational.ElementAtOrDefault(1),
            builtAt is null ? null : DateTimeOffset.Parse(builtAt, CultureInfo.InvariantCulture));
    }
}

/// <summary>Visible to integration tests.</summary>
public partial class Program;
