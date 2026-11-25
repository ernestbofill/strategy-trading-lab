using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// The backtest's data in the branch's object storage bucket, under <c>backtest/</c>: the latest draw (<c>draw.json</c>, with a dated
/// copy under <c>draws/</c>) and a snapshot a month (<c>snapshots/yyyy-MM/{run}/</c>: <c>manifest.json</c> and <c>prices/{symbol}.csv.gz</c>),
/// with the latest one's report in <c>snapshot.json</c>. Not in the <c>prices</c> table: a snapshot never changes once taken.
/// </summary>
public class BacktestStore(IServiceProvider services, IOptions<ObjectStorageOptions> options, IOptions<BacktestDataOptions> data)
{
    string Root => data.Value.StorageRoot;
    string DrawKey => Root + "draw.json";
    string SnapshotKey => Root + "snapshot.json";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    /// <summary>The S3 client, or null when the API runs without object storage (then nothing can be drawn).</summary>
    IAmazonS3? S3 => services.GetService<IAmazonS3>();

    public bool Configured => S3 is not null;

    string Bucket => options.Value.Bucket;

    /// <summary>
    /// A run's own folder in its month: a run that fails partway writes no file of the snapshot in use, which <c>snapshot.json</c> and
    /// its manifest name until a run finishes.
    /// </summary>
    public string SnapshotFolder(string month, DateTimeOffset run) => $"{Root}snapshots/{month}/{run:yyyyMMdd-HHmmss}/";

    /// <summary>A price file's key: the symbol escaped, so <c>^NDX</c> or <c>BRK-B</c> are safe in a path.</summary>
    public static string PriceKey(string folder, string symbol) => $"{folder}prices/{Uri.EscapeDataString(symbol)}.csv.gz";

    public Task<DrawReport?> DrawAsync(CancellationToken cancellationToken) => ReadAsync<DrawReport>(DrawKey, cancellationToken);
    public Task<SnapshotReport?> SnapshotAsync(CancellationToken cancellationToken) => ReadAsync<SnapshotReport>(SnapshotKey, cancellationToken);

    public async Task SaveDrawAsync(DrawReport draw, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(draw, Json);
        await PutAsync($"{Root}draws/{draw.DrawnAt:yyyy-MM-ddTHH-mm-ss}.json", bytes, "application/json", cancellationToken);
        await PutAsync(DrawKey, bytes, "application/json", cancellationToken);
    }

    /// <summary>Writes the manifest last, then the latest report: a snapshot interrupted midway is never the latest.</summary>
    public async Task SaveSnapshotAsync(SnapshotReport snapshot, object manifest, CancellationToken cancellationToken)
    {
        await PutAsync(snapshot.Key + "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, Json), "application/json", cancellationToken);
        await PutAsync(SnapshotKey, JsonSerializer.SerializeToUtf8Bytes(snapshot, Json), "application/json", cancellationToken);
    }

    public Task PutPricesAsync(string folder, string symbol, byte[] file, CancellationToken cancellationToken) =>
        PutAsync(PriceKey(folder, symbol), file, "application/gzip", cancellationToken);

    /// <summary>A price file of a snapshot, or null when it has none for the symbol.</summary>
    public async Task<byte[]?> PricesAsync(string folder, string symbol, CancellationToken cancellationToken) =>
        await GetAsync(PriceKey(folder, symbol), cancellationToken);

    /// <summary>The latest snapshot's manifest: its folder, and the draft per period.</summary>
    public async Task<SnapshotManifest?> ManifestAsync(SnapshotReport snapshot, CancellationToken cancellationToken) =>
        await ReadAsync<SnapshotManifest>(snapshot.Key + "manifest.json", cancellationToken);

    /// <summary>
    /// A stock, fund or exchange rate outside the snapshot, fetched from Yahoo when a test needed it and kept for the month
    /// (<c>fetched/yyyy-MM/</c>): the next month's tests fetch it again, as the snapshot is taken again.
    /// </summary>
    public string FetchedFolder(string month) => $"{Root}fetched/{month}/";

    public Task<FetchedAsset?> FetchedAsync(string month, string symbol, CancellationToken cancellationToken) =>
        ReadAsync<FetchedAsset>($"{FetchedFolder(month)}{Uri.EscapeDataString(symbol)}.json", cancellationToken);

    /// <summary>Writes the prices first and the description last: a description is only there once its prices are.</summary>
    public async Task SaveFetchedAsync(string month, FetchedAsset asset, byte[] prices, CancellationToken cancellationToken)
    {
        await PutPricesAsync(FetchedFolder(month), asset.Symbol, prices, cancellationToken);
        await PutAsync($"{FetchedFolder(month)}{Uri.EscapeDataString(asset.Symbol)}.json", JsonSerializer.SerializeToUtf8Bytes(asset, Json), "application/json", cancellationToken);
    }

    /// <summary>A test's result, kept by its key (<c>results/{key}.json</c>): the same rules, stocks, periods, money, costs and data give it back.</summary>
    public Task<byte[]?> ResultAsync(string key, CancellationToken cancellationToken) => GetAsync($"{Root}results/{key}.json", cancellationToken);

    public Task SaveResultAsync(string key, byte[] result, CancellationToken cancellationToken) =>
        PutAsync($"{Root}results/{key}.json", result, "application/json", cancellationToken);

    async Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        var s3 = S3 ?? throw new InvalidOperationException("Object storage isn't configured.");
        using var body = new MemoryStream(bytes);
        await s3.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = key, InputStream = body, ContentType = contentType }, cancellationToken);
    }

    async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (S3 is not { } s3) return null;
        try
        {
            using var response = await s3.GetObjectAsync(Bucket, key, cancellationToken);
            using var copy = new MemoryStream();
            await response.ResponseStream.CopyToAsync(copy, cancellationToken);
            return copy.ToArray();
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken) where T : class =>
        await GetAsync(key, cancellationToken) is { } bytes ? JsonSerializer.Deserialize<T>(bytes, Json) : null;
}

/// <summary>What a test reads of a snapshot's manifest: the periods drawn, with the draft of each.</summary>
public record SnapshotManifest(string Month, IReadOnlyList<ManifestPeriod> Periods);

public record ManifestPeriod(string Label, DateOnly Start, DateOnly End, bool Partial, IReadOnlyList<string> Draft);

/// <summary>A symbol fetched for a test: its currency and name, and its prices' first and last day (the prices are a file beside it).</summary>
public record FetchedAsset(string Symbol, string Currency, string? Name, DateOnly Since, DateOnly First, DateOnly Last);
