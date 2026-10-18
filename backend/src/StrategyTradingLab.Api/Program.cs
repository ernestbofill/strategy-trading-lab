using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Neon;

// Locally, `neon deploy` / `neon env pull` write the Neon variables to .env.local at the repo root.
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    NeonEnvironment.LoadDotEnv(AppContext.BaseDirectory);
    NeonEnvironment.LoadDotEnv(Directory.GetCurrentDirectory());
}

var builder = WebApplication.CreateBuilder(args);

var connectionString = NeonEnvironment.ConnectionString(builder.Configuration)
    ?? throw new InvalidOperationException(
        "No database: set DATABASE_URL (run `neon env pull`) or ConnectionStrings:Default.");
builder.Services.AddDbContext<AppDbContext>(options => AppDbContextOptions.Configure(options, connectionString));

builder.Services.AddOpenApi();

builder.Services.Configure<ObjectStorageOptions>(builder.Configuration.GetSection("ObjectStorage"));
if (ObjectStorage.IsConfigured(builder.Configuration))
{
    builder.Services.AddSingleton(_ => ObjectStorage.CreateClient(builder.Configuration));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document at /openapi/v1.json, browsable at /swagger.
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Strategy Trading Lab API"));
}

app.UseHttpsRedirection();

// Checks each Neon service the API depends on can be reached.
app.MapGet("/health", async (AppDbContext db, IServiceProvider services, IOptions<ObjectStorageOptions> storage,
    CancellationToken cancellationToken) =>
{
    var postgres = await db.Database
        .SqlQueryRaw<string>("select version() as \"Value\"")
        .SingleAsync(cancellationToken);

    var s3 = services.GetService<IAmazonS3>();
    string objectStorage;
    if (s3 is null)
    {
        objectStorage = "not configured";
    }
    else
    {
        var listing = await s3.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = storage.Value.Bucket, MaxKeys = 1 }, cancellationToken);
        objectStorage = $"ok: bucket {storage.Value.Bucket} ({listing.KeyCount ?? 0} key(s) in the first page)";
    }

    return TypedResults.Ok(new HealthResponse(postgres, objectStorage));
})
.WithName("Health")
.WithSummary("Checks the API can reach Postgres and object storage on its Neon branch.");

app.Run();

record HealthResponse(string Postgres, string ObjectStorage);

/// <summary>Visible to integration tests.</summary>
public partial class Program;
