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
using Microsoft.AspNetCore.Identity;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Assets;
using StrategyTradingLab.Api.Ai;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Invites;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Feedback;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Neon;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Signals;
using StrategyTradingLab.Api.Templates;

var builder = WebApplication.CreateBuilder(args);

// Locally, `neon deploy` / `neon env pull` write the Neon variables to .env.local at the repo root.
if (builder.Environment.IsDevelopment()) builder.Configuration.AddNeonDotEnv(AppContext.BaseDirectory);

// Read when a context is created, so a switch on the admin page applies to the next request (and test configuration applies).
builder.Services.AddSingleton<DatabaseSwitch>();
builder.Services.AddDbContext<AppDbContext>((services, options) => AppDbContextOptions.Configure(options,
    services.GetRequiredService<DatabaseSwitch>().ConnectionString()
    ?? throw new InvalidOperationException(
        "No database: set DATABASE_URL (run `neon env pull`) or ConnectionStrings:Default.")));

builder.Services.TryAddSingleton(TimeProvider.System);

// Keys that encrypt the admin page's secrets and its cookie. On the server, in the systemd StateDirectory (kept
// across releases, readable only by the service); elsewhere, Data Protection's default per-user folder.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("StrategyTradingLab");
if (builder.Configuration["STATE_DIRECTORY"] is { Length: > 0 } state)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(state, "keys")));
else if (!builder.Environment.IsDevelopment())
    // Under the unit's ProtectSystem=strict the default folder can't be written: keys would live in memory only and
    // every restart would lose the saved secrets. Fail the deploy's health check instead.
    throw new InvalidOperationException("STATE_DIRECTORY isn't set: run the API from its systemd unit (StateDirectory=).");
builder.Services.AddScoped<SettingsStore>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();

// Daily prices and exchange rates from Yahoo, refreshed on the schedule set on the admin page (PRICES_* settings).
builder.Services.Configure<PriceRefreshOptions>(builder.Configuration.GetSection("PriceRefresh"));
builder.Services.AddHttpClient<IMarketDataSource, YahooClient>(client =>
{
    client.BaseAddress = new Uri(YahooClient.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; StrategyTradingLab)");
});
builder.Services.AddScoped<PriceRefresher>();
builder.Services.AddScoped<AssetCatalog>();
builder.Services.AddScoped<StrategyTradingLab.Api.Evaluation.StrategyEvaluator>();
builder.Services.AddSingleton<PriceRefreshState>();
builder.Services.AddHostedService<PriceRefreshWorker>();
builder.Services.AddScoped<StrategyTradingLab.Api.Notifications.NotificationService>();
builder.Services.AddScoped<AiRounds>();
builder.Services.AddHostedService<StrategyTradingLab.Api.Notifications.NotificationWorker>();

// Accounts (backend/AUTH.md).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Sessions>();
builder.Services.AddSessionAuthorization();
builder.Services.AddSingleton<LogInThrottle>();
builder.Services.AddSingleton<TestEmailLimit>();
builder.Services.AddSingleton<TestYahooLimit>();
builder.Services.AddSingleton<InviteEmailLimit>();
builder.Services.AddSingleton<InviteTexts>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccountEmails>();

// nginx (on the same machine) passes the client's address and https on. Only loopback proxies are trusted
// (ForwardedHeadersOptions' default), so a client can't fake its address to dodge the log-in rate limit.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Accounts, per client: guessing, sign-up spam and email floods all hit these first.
    // RateLimits:<policy> overrides the permits; only the tests set it, to sign up many users.
    void PerClient(string policy, int permits, TimeSpan window) => options.AddPolicy(policy, http =>
        RateLimitPartition.GetFixedWindowLimiter(AdminEndpoints.ClientKey(http.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue($"RateLimits:{policy}", permits), Window = window,
            }));
    PerClient(AccountEndpoints.SignUpLimit, 5, TimeSpan.FromHours(1));
    PerClient(AccountEndpoints.LogInLimit, 10, TimeSpan.FromMinutes(15));
    PerClient(AccountEndpoints.EmailLimit, 5, TimeSpan.FromHours(1));
    PerClient(AccountEndpoints.PasswordLimit, 10, TimeSpan.FromMinutes(15));
    PerClient(FeedbackEndpoints.SubmitLimit, 30, TimeSpan.FromHours(1));
    // A look-up with a code tells whether it works, so it is limited like a guess; one without a code (every Sign-up
    // load, many visitors behind one address) only says whether sign-ups are invite only, and isn't limited.
    options.AddPolicy(AccountEndpoints.InviteLimit, http => string.IsNullOrEmpty(http.Request.Query["invite"])
        ? RateLimitPartition.GetNoLimiter("no-code")
        : RateLimitPartition.GetFixedWindowLimiter(AdminEndpoints.ClientKey(http.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue($"RateLimits:{AccountEndpoints.InviteLimit}", 30), Window = TimeSpan.FromHours(1),
            }));
    // Asset search and adding, per user: both call the price source, and adding loads a whole price history.
    void PerUser(string policy, int permits, TimeSpan window) => options.AddPolicy(policy, http =>
        RateLimitPartition.GetFixedWindowLimiter(AssetEndpoints.UserKey(http),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue($"RateLimits:{policy}", permits), Window = window,
            }));
    PerUser(AssetEndpoints.SearchLimit, 100, TimeSpan.FromHours(1));
    PerUser(AssetEndpoints.AddLimit, 10, TimeSpan.FromHours(1));
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

// CSRF (backend/AUTH.md): every state-changing request must carry X-Requested-With: stl, which the app always sends
// and a form or image on another site can't. With SameSite=Lax cookies and JSON-only bodies, that closes CSRF.
app.Use(async (http, next) =>
{
    if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && !HttpMethods.IsOptions(http.Request.Method)
        && http.Request.Headers["X-Requested-With"] != "stl")
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        await http.Response.WriteAsJsonAsync(new { title = "Missing the X-Requested-With: stl header." });
        return;
    }
    await next(http);
});
// After the forwarded headers (the session cookie's Secure flag) and the CSRF check.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// A database switched to on the admin page must still have every migration after a release. In the background, so
// the API starts listening (and passes the deploy's health check) without waiting on it.
var databases = app.Services.GetRequiredService<DatabaseSwitch>();
_ = databases.CheckActiveAsync(app.Lifetime.ApplicationStopping);

// The launch profiles set this, so running the API locally brings the dev database up to date.
// Tests, cloud sessions (scripts/migrate-dev-https.py) and production (the deploy's migrations bundle) don't use it.
// Only the home database: a local API switched to production must never migrate it (the deploy does).
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:MigrateOnStartup") && databases.Active == databases.Home)
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
app.MapSignals();
app.MapAiRounds();
app.MapTrades();
app.MapAdmin();
app.MapInvites();
app.MapAccount();
app.MapFeedback();
app.MapAssets();

// For the client to show which API it talks to, and a DEV tag while it uses the dev database.
app.MapGet("/version", (DatabaseSwitch databases) => TypedResults.Ok(VersionResponse.Current with { Database = databases.Active }))
.WithName("Version")
.WithSummary("The API's version, the commit it was built from, when it was built (Release builds) and which database it uses.");

app.Run();

record HealthResponse(string Postgres, string ObjectStorage);

/// <summary>
/// Version from backend/Directory.Build.props; Commit and BuiltAt are stamped into the assembly at build time.
/// BuiltAt is null in Debug builds, where stamping it would rebuild the project every time. Database is the one in use.
/// </summary>
record VersionResponse(string Version, string? Commit, DateTimeOffset? BuiltAt, DatabaseName Database = default)
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
