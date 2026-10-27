using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using StrategyTradingLab.Api.Auth;

namespace StrategyTradingLab.Api.Ai;

/// <summary>A stock's scores as read from the answer. Source scores are null when the answer has no such column.</summary>
public record AiScore(Guid AssetId, decimal Overall, decimal? News, decimal? Mood, decimal? Figures, string Reason);

/// <summary>What was read from an answer: one score per stock found, and what stops it from being used.</summary>
public record AiReading(IReadOnlyList<AiScore> Scores, IReadOnlyList<string> Problems)
{
    /// <summary>Every stock scored once, every score from 0 to 100, nothing else.</summary>
    public bool Ok => Problems.Count == 0 && Scores.Count > 0;
}

/// <summary>
/// Reads the scores block of the AI's answer (spec: AI round, Behaviour): a Markdown table with a stock column and an overall
/// column (the prompt asks for one), or JSON (an array of objects, or an object holding one). It checks that every stock
/// listed is scored once, from 0 to 100, and that no other stock is. The answer is data only: the reader takes numbers
/// and short reasons from the scores block and nothing else in the text does anything. Pure: no database.
/// </summary>
public static partial class AiAnswerReader
{
    /// <summary>The longest answer read (and stored): a table for 60 stocks with long reasons fits many times over.</summary>
    public const int MaxLength = 50_000;

    /// <summary>The longest reason kept, in characters.</summary>
    public const int MaxReason = 200;

    const int MaxProblems = 12;

    record Row(string Stock, string? Overall, string? News, string? Mood, string? Figures, string? Reason);

    /// <summary>Reads <paramref name="answer"/>; its problems are in <paramref name="locale"/>.</summary>
    public static AiReading Read(string? answer, IReadOnlyList<AiStock> stocks, string locale = "en")
    {
        if (string.IsNullOrWhiteSpace(answer)) return new([], [Messages.PasteAnswer.In(locale)]);
        if (answer.Length > MaxLength) return new([], [Messages.AnswerTooLong.In(locale)]);

        var rows = TableRows(answer) ?? JsonRows(answer);
        if (rows is null || rows.Count == 0)
            return new([], [Messages.NoScoresTable.In(locale)]);

        var problems = new List<string>();
        var scores = new Dictionary<Guid, AiScore>();
        var refused = new HashSet<Guid>();
        foreach (var row in rows)
        {
            if (Match(row.Stock, stocks) is not { } stock)
            {
                problems.Add(Messages.NotYourStock.In(locale, Shorten(Clean(row.Stock), 40)));
                continue;
            }
            if (scores.ContainsKey(stock.AssetId) || refused.Contains(stock.AssetId))
            {
                problems.Add(Messages.ScoredTwice.In(locale, stock.Name));
                continue;
            }
            if (Score(row.Overall) is not { } overall)
            {
                problems.Add(Messages.BadScore.In(locale, stock.Name));
                refused.Add(stock.AssetId);
                continue;
            }
            scores[stock.AssetId] = new AiScore(stock.AssetId, overall, Score(row.News), Score(row.Mood), Score(row.Figures),
                Shorten(Clean(row.Reason ?? ""), MaxReason));
        }
        // A stock refused above has its own problem already.
        foreach (var stock in stocks.Where(s => !scores.ContainsKey(s.AssetId) && !refused.Contains(s.AssetId)))
            problems.Add(Messages.NoScore.In(locale, stock.Name));

        var shown = problems.Count > MaxProblems
            ? [.. problems.Take(MaxProblems - 1), Messages.MoreProblems.In(locale, problems.Count - (MaxProblems - 1))]
            : problems;
        // In the stocks' order, so the screen lists them as the prompt did.
        return new([.. stocks.Where(s => scores.ContainsKey(s.AssetId)).Select(s => scores[s.AssetId])], shown);
    }

    /// <summary>
    /// The first Markdown table whose header has a stock column and an overall column, up to the first line that isn't part
    /// of it; null when there is none.
    /// </summary>
    static List<Row>? TableRows(string answer)
    {
        var lines = answer.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains('|')) continue;
            var header = Cells(lines[i]).Select(Key).ToList();
            var stock = header.FindIndex(IsStock);
            var overall = header.FindIndex(IsOverall);
            if (stock < 0 || overall < 0 || stock == overall) continue;
            int news = header.FindIndex(IsNews), mood = header.FindIndex(IsMood),
                figures = header.FindIndex(IsFigures), reason = header.FindIndex(IsReason);

            var rows = new List<Row>();
            for (var j = i + 1; j < lines.Length; j++)
            {
                var line = lines[j].Trim();
                if (line.Length == 0)
                {
                    if (rows.Count > 0) break;
                    continue;
                }
                if (!line.Contains('|')) break;
                var cells = Cells(line);
                if (cells.All(c => SeparatorCell().IsMatch(c))) continue; // |---|:--:|
                string? At(int index) => index >= 0 && index < cells.Count ? cells[index] : null;
                if (At(stock) is not { } name || Clean(name).Length == 0) continue;
                rows.Add(new Row(name, At(overall), At(news), At(mood), At(figures), At(reason)));
            }
            // A header with no rows under it (the prompt's own, quoted back): the table may come later.
            if (rows.Count > 0) return rows;
        }
        return null;
    }

    static List<string> Cells(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|')) trimmed = trimmed[1..];
        if (trimmed.EndsWith('|')) trimmed = trimmed[..^1];
        return [.. trimmed.Split('|').Select(cell => cell.Trim())];
    }

    /// <summary>The objects of a JSON array in the answer (in a code fence or bare), or of the first array an object holds.</summary>
    static List<Row>? JsonRows(string answer)
    {
        var fenced = JsonFence().Match(answer);
        var bytes = System.Text.Encoding.UTF8.GetBytes(fenced.Success ? fenced.Groups[1].Value : answer);
        // Prose around the JSON can hold brackets of its own ("[1]"): try each opening bracket until one gives rows.
        var offset = 0;
        for (var attempt = 0; attempt < MaxJsonStarts; attempt++)
        {
            var next = bytes.AsSpan(offset).IndexOfAny((byte)'[', (byte)'{');
            if (next < 0) return null;
            var start = offset + next;
            offset = start + 1;

            JsonDocument document;
            try
            {
                var reader = new Utf8JsonReader(bytes.AsSpan(start), new JsonReaderOptions { MaxDepth = 8, AllowTrailingCommas = true });
                if (!JsonDocument.TryParseValue(ref reader, out document!)) continue;
            }
            catch (JsonException)
            {
                continue;
            }
            using (document)
            {
                var rows = JsonObjects(document.RootElement);
                if (rows.Count > 0) return rows;
            }
        }
        return null;
    }

    const int MaxJsonStarts = 50;

    static List<Row> JsonObjects(JsonElement root)
    {
        var items = root.ValueKind == JsonValueKind.Array ? root
            : root.ValueKind == JsonValueKind.Object
                ? root.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Array)
                : default;
        var rows = new List<Row>();
        if (items.ValueKind != JsonValueKind.Array) return rows;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var fields = item.EnumerateObject()
                .Where(p => p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                .Select(p => (Key: Key(p.Name), Value: p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText()))
                .ToList();
            string? Field(Func<string, bool> test) => fields.FirstOrDefault(f => test(f.Key)).Value;
            if (Field(IsStock) is not { } name) continue;
            rows.Add(new Row(name, Field(IsOverall), Field(IsNews), Field(IsMood), Field(IsFigures), Field(IsReason)));
        }
        return rows;
    }

    /// <summary>A column's name in lower-case letters only: "Overall score" → "overallscore".</summary>
    static string Key(string name) => new([.. Clean(name).ToLowerInvariant().Where(char.IsAsciiLetterLower)]);

    // English, and Catalan for an answer in it ("Acció", "Global", "Motiu"; accents are dropped by Key).
    static bool IsStock(string key) =>
        key.Contains("stock") || key.Contains("company") || key.Contains("ticker") || key.Contains("symbol") || key == "name"
        || key.StartsWith("acci", StringComparison.Ordinal) || key.Contains("empresa") || key.StartsWith("smbol", StringComparison.Ordinal);

    static bool IsOverall(string key) => key.Contains("overall") || key is "total" or "score" or "totalscore" || key.Contains("global") || key.Contains("puntuaci");

    static bool IsMood(string key) => key.Contains("mood") || key.Contains("sentiment") || key.Contains("nim");

    static bool IsFigures(string key) =>
        key.Contains("figure") || key.Contains("financial") || key.Contains("fundamental") || key.Contains("xifres");

    static bool IsReason(string key) =>
        key.Contains("reason") || key.Contains("why") || key.Contains("rationale") || key.Contains("comment") || key.Contains("motiu");

    static bool IsNews(string key) => key.Contains("news") || key.Contains("notcies") || key.Contains("noticies");

    /// <summary>
    /// A score from 0 to 100 at the start of a cell: "83", "83/100", "**83**", "82.5". Null for anything else, a score on another
    /// scale ("8/10") included.
    /// </summary>
    static decimal? Score(string? cell)
    {
        if (cell is null) return null;
        var match = ScoreCell().Match(Clean(cell));
        if (!match.Success) return null;
        var value = decimal.Parse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return value is >= 0 and <= 100 ? Math.Round(value, 1) : null;
    }

    /// <summary>
    /// The stock a cell names: its symbol ("ZS", "SU.PA"), the symbol in brackets or before a colon or dash ("Zscaler (ZS)",
    /// "ZS: Zscaler"), the symbol without its exchange ("SU"), or its name ("Zscaler", "Zscaler Inc."). Null unless exactly one fits.
    /// </summary>
    static AiStock? Match(string cell, IReadOnlyList<AiStock> stocks)
    {
        var text = Clean(cell);
        var tokens = new List<string> { text };
        var bracket = Bracketed().Match(text);
        if (bracket.Success) tokens.Add(bracket.Groups[1].Value.Trim());
        var lead = LeadingSymbol().Match(text);
        if (lead.Success) tokens.Add(lead.Groups[1].Value);

        AiStock? Only(Func<AiStock, bool> test) => stocks.Where(test).Take(2).ToList() is [var one] ? one : null;

        return Only(s => tokens.Exists(t => t.Equals(s.Symbol, StringComparison.OrdinalIgnoreCase)))
            ?? Only(s => tokens.Exists(t => t.Equals(BaseSymbol(s.Symbol), StringComparison.OrdinalIgnoreCase)))
            ?? Only(s => SameName(text, s.Name) || (bracket.Success && SameName(text[..bracket.Index], s.Name)));
    }

    static string BaseSymbol(string symbol) => symbol.Split('.')[0];

    /// <summary>Equal once suffixes and punctuation are gone ("Zscaler, Inc." is Zscaler), or one starts with the other ("Monolithic Power").</summary>
    static bool SameName(string text, string name)
    {
        string Simple(string s) => CompanySuffix().Replace(new string([.. s.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ')]), " ")
            .Replace(" ", "");
        var (a, b) = (Simple(text), Simple(name));
        if (a.Length == 0 || b.Length == 0) return false;
        return a == b || (Math.Min(a.Length, b.Length) >= 4 && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)));
    }

    /// <summary>The text without Markdown emphasis, code marks, links' targets or control characters.</summary>
    static string Clean(string text)
    {
        var plain = MarkdownLink().Replace(text, "$1");
        plain = new string([.. plain.Where(c => !char.IsControl(c) && c is not ('*' or '`' or '_'))]);
        return Whitespace().Replace(plain, " ").Trim();
    }

    static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    [GeneratedRegex(@"^:?-{2,}:?$")]
    private static partial Regex SeparatorCell();

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonFence();

    [GeneratedRegex(@"^\[?\s*(\d{1,3}(?:[.,]\d+)?)(?![.,]?\d)\s*\]?\s*(?!\s*/\s*(?!100\b))(?:/\s*100|%|points?)?\b")]
    private static partial Regex ScoreCell();

    [GeneratedRegex(@"\(([^()]{1,20})\)")]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"^([A-Za-z0-9.\-]{1,15})\s*[:–—-]\s+\S")]
    private static partial Regex LeadingSymbol();

    [GeneratedRegex(@"\b(inc|incorporated|corp|corporation|co|company|plc|ltd|limited|sa|se|nv|ag|holdings?|group|class [abc])\b")]
    private static partial Regex CompanySuffix();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
