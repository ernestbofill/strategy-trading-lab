using System.Globalization;
using System.Text;
using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>A signal as the alert and the overview describe it.</summary>
public record SignalLine(Guid StrategyId, string StrategyName, string TemplateKey, string StockName, string AssetCurrency, string StrategyCurrency,
    DateOnly Date, decimal ClosePrice, int? StepNo, int? MaxSteps, decimal? StepAmount, JsonElement Facts);

/// <summary>A stock a strategy is watching and how far it is from the rules.</summary>
public record CloseLine(string StrategyName, string TemplateKey, string StockName, decimal DistancePct);

/// <summary>One strategy in the scoreboard.</summary>
public record ScoreLine(string Name, StrategyMode Mode, DateOnly StartedOn, int Signals);

/// <summary>
/// The words of the notification emails (spec: Notifications), in English until the Catalan texts exist. They describe
/// what the rules found and never tell the user what to do ("describe, don't instruct"). Pure: data in, text out.
/// </summary>
public static class EmailTexts
{
    public const string Disclaimer = "Not financial advice. Signals follow your rules.";

    static string Money(decimal amount, string currency) =>
        (currency switch { "EUR" => "€", "USD" => "$", "GBP" => "£", _ => "" }) + amount.ToString("#,0.##", CultureInfo.InvariantCulture);

    /// <summary>A number with a dot, whatever the server's culture (the texts are English).</summary>
    static string Pct(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    static string Date(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    static decimal? Fact(JsonElement facts, string name) =>
        facts.ValueKind == JsonValueKind.Object && facts.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    /// <summary>The rule facts of a signal in a sentence, by template.</summary>
    public static string FactSentence(SignalLine s)
    {
        if (s.TemplateKey == "dip")
        {
            var below = Fact(s.Facts, "belowPeakPct");
            return below is null ? "its rules are met" : $"{Pct(below.Value)}% below its recent peak, still above its 50-day average";
        }
        var milestone = s.Facts.ValueKind == JsonValueKind.Object && s.Facts.TryGetProperty("milestone", out var m) ? m.GetString() : null;
        if (milestone is not null) return $"a new sign that the recovery is real: {milestone.ToLowerInvariant()}";
        var above = Fact(s.Facts, "aboveBuyLinePct");
        return above is null ? "its rules are met" : $"{Pct(Math.Abs(above.Value))}% {(above >= 0 ? "above" : "below")} its buy line, on rising volume";
    }

    /// <summary>The signal box's text: "Your rules for X are met for Y: …".</summary>
    public static string SignalText(SignalLine s)
    {
        var step = s.StepNo is { } no && s.MaxSteps is { } max
            ? $" Under your settings that's step {no} of {max}{(s.StepAmount is { } amount ? $" ({Money(amount, s.StrategyCurrency)})" : "")}."
            : "";
        return $"Your rules for {s.StrategyName} are met for {s.StockName}: {FactSentence(s)}.{step}\n" +
               $"Close on {Date(s.Date)}: {Money(s.ClosePrice, s.AssetCurrency)}";
    }

    public static (string Subject, string Body) SignalAlert(SignalLine s, string appUrl) =>
        ($"{s.StockName}: your rules for {s.StrategyName} are met",
         $"{SignalText(s)}\n\nSee it, and say whether you acted on it:\n{appUrl}/strategies/{s.StrategyId}/signal\n\n{Disclaimer}");

    /// <summary>The daily overview: what waits, what changed, what is close, and one line per strategy.</summary>
    public static (string Subject, string Body) Overview(DateOnly day, IReadOnlyList<SignalLine> waiting, IReadOnlyList<SignalLine> changed,
        IReadOnlyList<CloseLine> close, IReadOnlyList<ScoreLine> scoreboard, string appUrl)
    {
        var text = new StringBuilder($"Your daily overview, {Date(day)}.\n");
        if (waiting.Count > 0)
        {
            text.Append("\nWaiting for you\n");
            foreach (var s in waiting) text.Append($"- {s.StockName}, {s.StrategyName}: step {s.StepNo} of {s.MaxSteps}, close on {Date(s.Date)}. {appUrl}/strategies/{s.StrategyId}/signal\n");
        }
        text.Append("\nWhat changed today\n");
        if (changed.Count == 0) text.Append("- No rules were newly met.\n");
        foreach (var s in changed) text.Append($"- {SignalText(s).Replace("\n", " ")}\n");
        if (close.Count > 0)
        {
            text.Append("\nClose to the line\n");
            foreach (var c in close)
                text.Append($"- {c.StockName}, {c.StrategyName}: {Pct(c.DistancePct)}% {(c.TemplateKey == "dip" ? "above the discount zone" : "below the buy line")}\n");
        }
        text.Append("\nYour strategies\n");
        foreach (var s in scoreboard)
            text.Append($"- {s.Name} ({(s.Mode == StrategyMode.Live ? "Live" : "Simulation")}): since {Date(s.StartedOn)}, {s.Signals} {(s.Signals == 1 ? "signal" : "signals")}\n");
        text.Append($"\nOpen the app: {appUrl}/\n\n{Disclaimer}");
        return ($"Your daily overview, {Date(day)}", text.ToString());
    }
}
