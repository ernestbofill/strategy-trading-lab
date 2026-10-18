using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Neon;

var builder = WebApplication.CreateBuilder(args);

// Locally, `neon deploy` / `neon env pull` write the Neon variables to .env.local at the repo root.
if (builder.Environment.IsDevelopment()) builder.Configuration.AddNeonDotEnv(AppContext.BaseDirectory);

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
app.MapGet("/health", async (AppDbContext db, [FromServices] IAmazonS3? s3, IOptions<ObjectStorageOptions> storage,
    CancellationToken cancellationToken) =>
{
    var bucket = storage.Value.Bucket;
    var postgres = db.Database.SqlQueryRaw<string>("select version() as \"Value\"").SingleAsync(cancellationToken);
    var listing = s3?.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, MaxKeys = 1 }, cancellationToken);
    await Task.WhenAll(listing is null ? [postgres] : [postgres, listing]);

    var objectStorage = listing is null
        ? "not configured"
        : $"ok: bucket {bucket} ({listing.Result.KeyCount ?? 0} key(s) in the first page)";
    return TypedResults.Ok(new HealthResponse(postgres.Result, objectStorage));
})
.WithName("Health")
.WithSummary("Checks the API can reach Postgres and object storage on its Neon branch.");

app.Run();

record HealthResponse(string Postgres, string ObjectStorage);

/// <summary>Visible to integration tests.</summary>
public partial class Program;
