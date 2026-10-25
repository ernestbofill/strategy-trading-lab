using System.Globalization;
using System.Text;
using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>A signal as the alert and the overview describe it.</summary>
public record SignalLine(Guid StrategyId, Guid SignalId, string StrategyName, StrategyMode Mode, string TemplateKey, string StockName, string AssetCurrency, string StrategyCurrency,
    DateOnly Date, decimal ClosePrice, int? StepNo, int? MaxSteps, decimal? StepAmount, JsonElement Facts)
{
    public static SignalLine From(Signal signal, Strategy strategy, Asset asset) =>
        new(strategy.Id, signal.Id, strategy.Name, strategy.Mode, strategy.TemplateKey, asset.Name, asset.Currency, strategy.Currency, signal.SignalDate,
            signal.ClosePrice, signal.StepNo, signal.MaxSteps, signal.StepAmount, signal.Facts.RootElement.Clone());
}

/// <summary>A stock a strategy is watching and how far it is from the rules.</summary>
public record CloseLine(string StrategyName, StrategyMode Mode, string TemplateKey, string StockName, decimal DistancePct);

/// <summary>One strategy in the scoreboard.</summary>
public record ScoreLine(string Name, StrategyMode Mode, DateOnly StartedOn, int Signals);

/// <summary>
/// The words of the notification emails (spec: Notifications), in English and Catalan. They describe
/// what the rules found and never tell the user what to do ("describe, don't instruct"). Pure: data in, text out.
/// </summary>
public static class EmailTexts
{
    public const string DisclaimerEn = "Not financial advice. Signals follow your rules.";
    public const string DisclaimerCa = "No és assessorament financer. Els senyals segueixen les teves regles.";
    public const string Disclaimer = DisclaimerEn;

    static string Money(decimal amount, string currency) =>
        (currency switch { "EUR" => "€", "USD" => "$", "GBP" => "£", _ => currency + " " }) + amount.ToString("#,0.##", CultureInfo.InvariantCulture);

    /// <summary>A number with a dot, whatever the server's culture (the texts are English).</summary>
    static string Pct(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    static string Date(DateOnly date, string locale = "en")
    {
        if (locale == "ca")
        {
            var months = new[] { "", "gen.", "febr.", "març", "abr.", "maig", "juny", "jul.", "ag.", "set.", "oct.", "nov.", "des." };
            return $"{date.Day} {months[date.Month]} {date.Year}";
        }
        return date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    }

    static decimal? Fact(JsonElement facts, string name) =>
        facts.ValueKind == JsonValueKind.Object && facts.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    /// <summary>The rule facts of a signal in a sentence, by template.</summary>
    public static string FactSentence(SignalLine s, string locale = "en")
    {
        if (locale == "ca")
        {
            if (s.TemplateKey == "dip")
            {
                var below = Fact(s.Facts, "belowPeakPct");
                return below is null ? "es compleixen les seves regles" : $"{Pct(below.Value)}% per sota del seu màxim recent, encara per sobre de la mitjana de 50 dies";
            }
            var milestone = s.Facts.ValueKind == JsonValueKind.Object && s.Facts.TryGetProperty("milestone", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            if (milestone is not null) return $"un nou senyal que la recuperació és real: {milestone.ToLowerInvariant()}";
            var above = Fact(s.Facts, "aboveBuyLinePct");
            return above is null ? "es compleixen les seves regles" : $"{Pct(Math.Abs(above.Value))}% {(above >= 0 ? "per sobre" : "per sota")} de la línia de compra, amb volum creixent";
        }
        if (s.TemplateKey == "dip")
        {
            var below = Fact(s.Facts, "belowPeakPct");
            return below is null ? "its rules are met" : $"{Pct(below.Value)}% below its recent peak, still above its 50-day average";
        }
        var milestoneEn = s.Facts.ValueKind == JsonValueKind.Object && s.Facts.TryGetProperty("milestone", out var me) && me.ValueKind == JsonValueKind.String ? me.GetString() : null;
        if (milestoneEn is not null) return $"a new sign that the recovery is real: {milestoneEn.ToLowerInvariant()}";
        var aboveEn = Fact(s.Facts, "aboveBuyLinePct");
        return aboveEn is null ? "its rules are met" : $"{Pct(Math.Abs(aboveEn.Value))}% {(aboveEn >= 0 ? "above" : "below")} its buy line, on rising volume";
    }

    /// <summary>The signal box's text: "Your rules for X are met for Y: …".</summary>
    public static string SignalText(SignalLine s, string locale = "en")
    {
        if (locale == "ca")
        {
            var step = s.StepNo is { } no && s.MaxSteps is { } max
                ? $" Segons la teva configuració, és el pas {no} de {max}{(s.StepAmount is { } amtCa ? $" ({Money(amtCa, s.StrategyCurrency)})" : "")}."
                : "";
            return $"Les teves regles per a {s.StrategyName} es compleixen per a {s.StockName}: {FactSentence(s, locale)}.{step}\n" +
                   $"Tancament del {Date(s.Date, locale)}: {Money(s.ClosePrice, s.AssetCurrency)}";
        }
        var stepEn = s.StepNo is { } noEn && s.MaxSteps is { } maxEn
            ? $" Under your settings that's step {noEn} of {maxEn}{(s.StepAmount is { } amtEn ? $" ({Money(amtEn, s.StrategyCurrency)})" : "")}."
            : "";
        return $"Your rules for {s.StrategyName} are met for {s.StockName}: {FactSentence(s)}.{stepEn}\n" +
               $"Close on {Date(s.Date)}: {Money(s.ClosePrice, s.AssetCurrency)}";
    }

    /// <summary>The signal's Confirm a signal page.</summary>
    public static string SignalUrl(SignalLine s, string appUrl) => $"{appUrl}/strategies/{s.StrategyId}/signals/{s.SignalId}";

    /// <summary>The reminder the day after a snooze: the same text as the alert.</summary>
    public static (string Subject, string Body) SnoozeReminder(SignalLine s, string appUrl, string locale = "en")
    {
        if (locale == "ca")
            return ($"Recordatori: {s.StockName}, les teves regles per a {s.StrategyName}",
                $"Has demanat que et recordem aquest senyal.\n\n{SignalText(s, locale)}\n\nConsulta'l i indica si hi has actuat:\n{SignalUrl(s, appUrl)}\n\n{DisclaimerCa}");

        return ($"Reminder: {s.StockName}, your rules for {s.StrategyName}",
            $"You asked to be reminded of this signal.\n\n{SignalText(s)}\n\nSee it, and say whether you acted on it:\n{SignalUrl(s, appUrl)}\n\n{DisclaimerEn}");
    }

    public static (string Subject, string Body) SignalAlert(SignalLine s, string appUrl, string locale = "en")
    {
        if (locale == "ca")
            return ($"{s.StockName}: es compleixen les teves regles per a {s.StrategyName}",
                $"{SignalText(s, locale)}\n\nConsulta'l i indica si hi has actuat:\n{SignalUrl(s, appUrl)}\n\n{DisclaimerCa}");

        return ($"{s.StockName}: your rules for {s.StrategyName} are met",
            $"{SignalText(s)}\n\nSee it, and say whether you acted on it:\n{SignalUrl(s, appUrl)}\n\n{DisclaimerEn}");
    }

    /// <summary>The daily overview: what waits, what changed, what is close, and one line per strategy.</summary>
    public static (string Subject, string Body) Overview(DateOnly day, IReadOnlyList<SignalLine> waiting, IReadOnlyList<SignalLine> changed,
        IReadOnlyList<CloseLine> close, IReadOnlyList<ScoreLine> scoreboard, string appUrl, string locale = "en")
    {
        if (locale == "ca")
        {
            var text = new StringBuilder($"El teu resum diari, {Date(day, locale)}.\n");
            if (waiting.Count > 0)
            {
                text.Append("\nT’espera a tu\n");
                foreach (var s in waiting)
                    text.Append($"- {s.StockName}, {s.StrategyName}{(s.StepNo is { } no && s.MaxSteps is { } max ? $": pas {no} de {max}" : "")}, tancament del {Date(s.Date, locale)}. {SignalUrl(s, appUrl)}\n");
            }
            text.Append("\nQuè ha canviat avui\n");
            if (changed.Count == 0) text.Append("- No s'ha complert cap regla nova.\n");
            foreach (var s in changed) text.Append($"- ({(s.Mode == StrategyMode.Live ? "En directe" : "Simulació")}) {SignalText(s, locale).Replace("\n", " ")}\n");
            if (close.Count > 0)
            {
                text.Append("\nA prop de la línia\n");
                foreach (var c in close)
                    text.Append($"- ({(c.Mode == StrategyMode.Live ? "En directe" : "Simulació")}) {c.StockName}, {c.StrategyName}: {Pct(c.DistancePct)}% {(c.TemplateKey == "dip" ? "per sobre de la zona de descompte" : "per sota de la línia de compra")}\n");
            }
            text.Append("\nLes teves estratègies\n");
            foreach (var s in scoreboard)
                text.Append($"- {s.Name} ({(s.Mode == StrategyMode.Live ? "En directe" : "Simulació")}): des del {Date(s.StartedOn, locale)}, {s.Signals} {(s.Signals == 1 ? "senyal" : "senyals")}\n");
            text.Append($"\nObre l'aplicació: {appUrl}/\n\n{DisclaimerCa}");
            return ($"El teu resum diari, {Date(day, locale)}", text.ToString());
        }

        var textEn = new StringBuilder($"Your daily overview, {Date(day)}.\n");
        if (waiting.Count > 0)
        {
            textEn.Append("\nWaiting for you\n");
            foreach (var s in waiting)
                textEn.Append($"- {s.StockName}, {s.StrategyName}{(s.StepNo is { } no && s.MaxSteps is { } max ? $": step {no} of {max}" : "")}, close on {Date(s.Date)}. {SignalUrl(s, appUrl)}\n");
        }
        textEn.Append("\nWhat changed today\n");
        if (changed.Count == 0) textEn.Append("- No rules were newly met.\n");
        foreach (var s in changed) textEn.Append($"- ({(s.Mode == StrategyMode.Live ? "Live" : "Simulation")}) {SignalText(s).Replace("\n", " ")}\n");
        if (close.Count > 0)
        {
            textEn.Append("\nClose to the line\n");
            foreach (var c in close)
                textEn.Append($"- ({(c.Mode == StrategyMode.Live ? "Live" : "Simulation")}) {c.StockName}, {c.StrategyName}: {Pct(c.DistancePct)}% {(c.TemplateKey == "dip" ? "above the discount zone" : "below the buy line")}\n");
        }
        textEn.Append("\nYour strategies\n");
        foreach (var s in scoreboard)
            textEn.Append($"- {s.Name} ({(s.Mode == StrategyMode.Live ? "Live" : "Simulation")}): since {Date(s.StartedOn)}, {s.Signals} {(s.Signals == 1 ? "signal" : "signals")}\n");
        textEn.Append($"\nOpen the app: {appUrl}/\n\n{DisclaimerEn}");
        return ($"Your daily overview, {Date(day)}", textEn.ToString());
    }
}
