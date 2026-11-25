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
/// copy under <c>draws/</c>) and a snapshot a month (<c>snapshots/yyyy-MM/</c>: <c>manifest.json</c> and <c>prices/{symbol}.csv.gz</c>),
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

    public string SnapshotFolder(string month) => $"{Root}snapshots/{month}/";

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
