using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Neon;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>End to end against the linked Neon branch (Postgres and Object Storage).</summary>
public class NeonBranchTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [NeonFact]
    public async Task Database_is_postgres_15_or_later_with_the_migration_applied()
    {
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
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // A skip reason on a signal that isn't skipped.
        var error = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            // A random symbol, so it never collides with seeded assets.
            var asset = new Asset { Symbol = Guid.NewGuid().ToString("N")[..8], Exchange = "XNAS", Name = "Test asset", Kind = AssetKind.Stock, Currency = "USD" };
            var strategy = new Strategy
            {
                UserId = DevSeed.DevUserId, TemplateKey = "dip", Name = $"Test {Guid.NewGuid()}",
                Mode = StrategyMode.Live, Status = StrategyStatus.Running, StartedOn = new DateOnly(2026, 10, 16),
                Currency = "EUR", Budget = new Budget(3000, 300, 10),
                Params = JsonDocument.Parse("""{"zoneStartPct":5}"""),
            };
            db.AddRange(asset, strategy);
            db.Signals.Add(new Signal
            {
                StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 10,
                StepAmount = 300, SignalDate = new DateOnly(2026, 10, 16), ClosePrice = 180.25m,
                Facts = JsonDocument.Parse("{}"), Status = SignalStatus.Waiting,
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
        var s3 = factory.Services.GetRequiredService<IAmazonS3>();
        var bucket = factory.Services.GetRequiredService<IOptions<ObjectStorageOptions>>().Value.Bucket;
        var key = $"tests/{Guid.NewGuid()}.txt";

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket, Key = key, ContentBody = "hello from the API tests", ContentType = "text/plain",
        });
        try
        {
            using var response = await s3.GetObjectAsync(bucket, key);
            using var reader = new StreamReader(response.ResponseStream);
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
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<Health>("/health");

        Assert.StartsWith("PostgreSQL", health!.Postgres);
        Assert.StartsWith("ok: bucket assets", health.ObjectStorage);
    }

    [NeonFact]
    public async Task Every_template_asset_is_in_the_database()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = TemplateAssets.All.Select(asset => asset.Id).ToList();

        Assert.Equal(ids.Count, await db.Assets.CountAsync(asset => ids.Contains(asset.Id)));
    }

    [NeonFact]
    public async Task Templates_list_the_five_in_order()
    {
        using var client = factory.CreateClient();

        var templates = await client.GetFromJsonAsync<JsonElement>("/templates");

        Assert.Equal(TemplateCatalog.Keys,
            templates.EnumerateArray().Select(t => t.GetProperty("key").GetString()));
        Assert.Equal("buysAndSells", templates[3].GetProperty("signals").GetString());
        Assert.Equal(2, templates[4].GetProperty("aiRound").GetProperty("everyWeeks").GetInt32());
    }

    [NeonFact]
    public async Task A_template_has_its_starter_list_from_the_database_sorted_by_name()
    {
        using var client = factory.CreateClient();

        var dip = await client.GetFromJsonAsync<JsonElement>("/templates/dip");

        var watches = dip.GetProperty("watches");
        Assert.Equal("stocks", watches.GetProperty("kind").GetString());
        Assert.Equal(
            ["Eaton", "Modine", "Monolithic Power Systems", "nVent Electric", "Super Micro Computer", "Vertiv", "Vicor"],
            watches.GetProperty("starterList").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(60, dip.GetProperty("defaults").GetProperty("params").GetProperty("peakDays").GetInt32());
    }

    [NeonFact]
    public async Task A_fund_template_watches_one_fund()
    {
        using var client = factory.CreateClient();

        var trend = await client.GetFromJsonAsync<JsonElement>("/templates/trend");

        var watches = trend.GetProperty("watches");
        Assert.Equal("fund", watches.GetProperty("kind").GetString());
        Assert.Equal("SPY", watches.GetProperty("fund").GetProperty("symbol").GetString());
        Assert.Equal(JsonValueKind.Null, trend.GetProperty("defaults").GetProperty("budget").GetProperty("step").ValueKind);
    }

    [NeonFact]
    public async Task An_unknown_template_is_not_found()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/templates/nope");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    record Health(string Postgres, string ObjectStorage);
}
