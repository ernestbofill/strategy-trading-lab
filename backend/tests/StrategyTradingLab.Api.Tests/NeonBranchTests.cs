using System.Net.Http.Json;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Tests;

/// <summary>End to end against the linked Neon branch (Postgres and Object Storage).</summary>
public class NeonBranchTests
{
    [NeonFact]
    public async Task Database_is_postgres_15_or_later_with_the_migration_applied()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var versionNum = await db.Database.SqlQueryRaw<int>("select current_setting('server_version_num')::int as \"Value\"").SingleAsync();
        Assert.True(versionNum >= 150000, $"server_version_num {versionNum}");

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Users.AnyAsync(user => user.Id == DevSeed.DevUserId));
    }

    [NeonFact]
    public async Task A_check_constraint_rejects_an_impossible_signal()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // A skip reason on a signal that isn't skipped.
        var error = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            var asset = new Asset { Symbol = "ZS", Exchange = "XNAS", Name = "Zscaler", Kind = AssetKind.Stock, Currency = "USD" };
            var strategy = new Strategy
            {
                UserId = DevSeed.DevUserId, TemplateKey = "dip", Name = $"Test {Guid.NewGuid()}",
                Mode = StrategyMode.Live, Status = StrategyStatus.Running, StartedOn = new DateOnly(2026, 10, 16),
                Currency = "EUR", Budget = new Budget(3000, 300, 10),
                Params = System.Text.Json.JsonDocument.Parse("""{"zoneStartPct":5}"""),
            };
            db.AddRange(asset, strategy);
            db.Signals.Add(new Signal
            {
                StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 10,
                StepAmount = 300, SignalDate = new DateOnly(2026, 10, 16), ClosePrice = 180.25m,
                Facts = System.Text.Json.JsonDocument.Parse("{}"), Status = SignalStatus.Waiting,
                SkipReason = SkipReason.NoCash,
            });
            await db.SaveChangesAsync();
        });

        Assert.Contains("ck_signals_skip_reason_skipped", error.InnerException?.Message);
        await transaction.RollbackAsync();
    }

    [NeonFact]
    public async Task Object_storage_round_trips_an_object()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        using var s3 = ObjectStorage.CreateClient(configuration);
        var bucket = new ObjectStorageOptions().Bucket;
        var key = $"tests/{Guid.NewGuid()}.txt";

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket, Key = key, ContentBody = "hello from the API tests", ContentType = "text/plain",
        });
        try
        {
            using var response = await s3.GetObjectAsync(bucket, key);
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            Assert.Equal("hello from the API tests", await reader.ReadToEndAsync());
        }
        finally
        {
            await s3.DeleteObjectAsync(bucket, key);
        }

        await Assert.ThrowsAsync<AmazonS3Exception>(() => s3.GetObjectMetadataAsync(bucket, key));
    }

    [NeonFact]
    public async Task Health_reaches_postgres_and_object_storage()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<Health>("/health");

        Assert.StartsWith("PostgreSQL", health!.Postgres);
        Assert.StartsWith("ok: bucket assets", health.ObjectStorage);
    }

    record Health(string Postgres, string ObjectStorage);
}
