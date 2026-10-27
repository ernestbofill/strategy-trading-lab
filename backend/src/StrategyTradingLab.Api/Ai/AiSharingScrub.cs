using System.Text.RegularExpressions;

namespace StrategyTradingLab.Api.Ai;

/// <summary>
/// Before a shared round is stored (spec: AI data sharing): removes what looks like an email address or a phone number, and
/// the parameters of links (where a session or an account id would hide), keeping the address of the page itself.
/// </summary>
public static partial class AiSharingScrub
{
    public static string Scrub(string text)
    {
        var scrubbed = Email().Replace(text, "[email]");
        scrubbed = Link().Replace(scrubbed, match => match.Groups[1].Value);
        return Phone().Replace(scrubbed, match =>
        {
            // A phone number has 9 to 15 digits; dates, figures ("2026-10-27", "1,234,567") and scores in a row ("80 75 90 85")
            // are left alone.
            var digits = match.Value.Count(char.IsAsciiDigit);
            var scores = Groups().Matches(match.Value).All(g => g.Value.Length <= 3 && int.Parse(g.Value) <= 100);
            return digits is >= 9 and <= 15 && !scores && !IsoDate().IsMatch(match.Value) ? "[number]" : match.Value;
        });
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)+")]
    private static partial Regex Email();

    /// <summary>A link: group 1 is everything before its query string or fragment.</summary>
    [GeneratedRegex(@"(https?://[^\s?#)\]>""'|]+)[?#][^\s)\]>""'|]*", RegexOptions.IgnoreCase)]
    private static partial Regex Link();

    [GeneratedRegex(@"\+?\d[\d ().\-]{7,}\d")]
    private static partial Regex Phone();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Groups();
}
