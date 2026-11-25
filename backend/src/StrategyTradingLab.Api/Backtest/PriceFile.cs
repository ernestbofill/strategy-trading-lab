using System.Globalization;
using System.IO.Compression;
using System.Text;
using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// One ticker's prices in a snapshot: a gzip-compressed CSV, <c>date,open,high,low,close,volume</c>, adjusted for splits and dividends,
/// oldest first. Plain text, so a file can be opened and checked by hand.
/// </summary>
public static class PriceFile
{
    const string Header = "date,open,high,low,close,volume";

    public static byte[] Write(IReadOnlyList<PriceBar> bars)
    {
        var text = new StringBuilder(Header).Append('\n');
        foreach (var b in bars)
            text.Append(CultureInfo.InvariantCulture, $"{b.Date:yyyy-MM-dd},{b.Open},{b.High},{b.Low},{b.Close},{b.Volume}\n");
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            gzip.Write(Encoding.UTF8.GetBytes(text.ToString()));
        return output.ToArray();
    }

    public static IReadOnlyList<PriceBar> Read(byte[] file)
    {
        using var gzip = new GZipStream(new MemoryStream(file), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        if (reader.ReadLine() != Header) throw new InvalidDataException("Not a backtest price file.");
        var bars = new List<PriceBar>();
        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var f = line.Split(',');
            bars.Add(new(DateOnly.ParseExact(f[0], "yyyy-MM-dd", CultureInfo.InvariantCulture), decimal.Parse(f[1], CultureInfo.InvariantCulture),
                decimal.Parse(f[2], CultureInfo.InvariantCulture), decimal.Parse(f[3], CultureInfo.InvariantCulture),
                decimal.Parse(f[4], CultureInfo.InvariantCulture), long.Parse(f[5], CultureInfo.InvariantCulture)));
        }
        return bars;
    }
}
