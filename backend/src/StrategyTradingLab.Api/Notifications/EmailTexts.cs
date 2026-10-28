using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>A signal as the alert and the overview describe it.</summary>
public record SignalLine(Guid StrategyId, Guid SignalId, string StrategyName, StrategyMode Mode, string TemplateKey, string StockName, string AssetCurrency, string StrategyCurrency,
    DateOnly Date, decimal ClosePrice, int? StepNo, int? MaxSteps, decimal? StepAmount, JsonElement Facts, TradeSide Side = TradeSide.Buy)
{
    public static SignalLine From(Signal signal, Strategy strategy, Asset asset) =>
        new(strategy.Id, signal.Id, strategy.Name, strategy.Mode, strategy.TemplateKey, asset.Name, asset.Currency, strategy.Currency, signal.SignalDate,
            signal.ClosePrice, signal.StepNo, signal.MaxSteps, signal.StepAmount, signal.Facts.RootElement.Clone(), signal.Side);
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

    static string DisclaimerFor(string locale) => locale == "ca" ? DisclaimerCa : DisclaimerEn;

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

    /// <summary>A recovery milestone (named in English by the engine) in Catalan.</summary>
    static string MilestoneCa(string term) => term switch
    {
        "Reclaims the SMA 200" => "Recupera la SMA 200",
        "Golden cross (SMA 50 above SMA 200)" => "Creu daurada (SMA 50 per sobre de la SMA 200)",
        _ when Regex.Match(term, @"^Within ([\d.]+)% of the 52-week high$") is { Success: true } near => $"A menys d'un {near.Groups[1].Value}% del màxim de 52 setmanes",
        _ when Regex.Match(term, @"^Higher high after pulling back ([\d.]+)% \(breakout\)$") is { Success: true } up => $"Nou màxim després d'una correcció del {up.Groups[1].Value}% (ruptura)",
        _ => term,
    };

    /// <summary>The rule facts of a signal in a sentence, by template.</summary>
    public static string FactSentence(SignalLine s, string locale = "en")
    {
        var ca = locale == "ca";
        var met = ca ? "es compleixen les seves regles" : "its rules are met";
        if (s.Side == TradeSide.Sell)
        {
            var belowSma = Fact(s.Facts, "belowSmaPct");
            return belowSma is null ? met
                : ca ? $"el mercat ha tancat un {Pct(belowSma.Value)}% per sota de la seva mitjana de 200 dies"
                : $"the market closed {Pct(belowSma.Value)}% below its 200-day average";
        }
        if (s.TemplateKey == "ai")
        {
            var score = Fact(s.Facts, "score");
            var rank = Fact(s.Facts, "rank");
            if (score is null) return ca ? "la teva IA l'ha puntuat entre els primers" : "your AI scored it among the top";
            return ca
                ? $"la teva IA l'ha puntuat amb {Pct(score.Value)} sobre 100, {(rank is null or 1 ? "la puntuació més alta de la ronda" : $"el número {rank:0} de la ronda")}"
                : $"your AI scored it {Pct(score.Value)} out of 100, {(rank is null or 1 ? "the top score of the round" : $"number {rank:0} in the round")}";
        }
        if (s.TemplateKey == "trend")
        {
            var aboveSma = Fact(s.Facts, "aboveSmaPct");
            return aboveSma is null ? met
                : ca ? $"el mercat ha tancat un {Pct(aboveSma.Value)}% per sobre de la seva mitjana de 200 dies"
                : $"the market closed {Pct(aboveSma.Value)}% above its 200-day average";
        }
        if (s.TemplateKey == "bear")
        {
            var below = Fact(s.Facts, "belowDropPeakPct") ?? Fact(s.Facts, "belowHighPct");
            var above = Fact(s.Facts, "aboveLowPct");
            return below is null || above is null ? met
                : ca ? $"un {Pct(below.Value)}% per sota del seu màxim de 6 mesos i un {Pct(above.Value)}% per sobre del seu mínim"
                : $"down {Pct(below.Value)}% from its 6-month high and {Pct(above.Value)}% above its low";
        }
        if (s.TemplateKey == "dip")
        {
            var below = Fact(s.Facts, "belowPeakPct");
            return below is null ? met
                : ca ? $"{Pct(below.Value)}% per sota del seu màxim recent, encara per sobre de la mitjana de 50 dies"
                : $"{Pct(below.Value)}% below its recent peak, still above its 50-day average";
        }
        var milestone = s.Facts.ValueKind == JsonValueKind.Object && s.Facts.TryGetProperty("milestone", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        if (milestone is not null)
            return ca ? $"un nou senyal que la recuperació és real: {MilestoneCa(milestone).ToLowerInvariant()}" : $"a new sign that the recovery is real: {milestone.ToLowerInvariant()}";
        var aboveBuyLine = Fact(s.Facts, "aboveBuyLinePct");
        return aboveBuyLine is null ? met
            : ca ? $"{Pct(Math.Abs(aboveBuyLine.Value))}% {(aboveBuyLine >= 0 ? "per sobre" : "per sota")} de la línia de compra, amb volum creixent"
            : $"{Pct(Math.Abs(aboveBuyLine.Value))}% {(aboveBuyLine >= 0 ? "above" : "below")} its buy line, on rising volume";
    }

    /// <summary>The signal box's text: "Your rules for X are met for Y: …".</summary>
    public static string SignalText(SignalLine s, string locale = "en")
    {
        var ca = locale == "ca";
        var sale = s.Side == TradeSide.Sell;
        var amount = s.StepAmount is { } a ? $" ({Money(a, s.StrategyCurrency)})" : "";
        if (ca)
        {
            var step = s.StepNo is { } no && s.MaxSteps is { } max ? $" Segons la teva configuració, és el pas {no} de {max}{amount}." : "";
            return $"Les teves regles per a {s.StrategyName} es compleixen per a {(sale ? "una venda" : s.StockName)}: {FactSentence(s, locale)}.{step}\n" +
                   $"Tancament del {Date(s.Date, locale)}: {Money(s.ClosePrice, s.AssetCurrency)}";
        }
        var stepEn = s.StepNo is { } noEn && s.MaxSteps is { } maxEn ? $" Under your settings that's step {noEn} of {maxEn}{amount}." : "";
        return $"Your rules for {s.StrategyName} are met for {(sale ? "a sale" : s.StockName)}: {FactSentence(s)}.{stepEn}\n" +
               $"Close on {Date(s.Date)}: {Money(s.ClosePrice, s.AssetCurrency)}";
    }

    /// <summary>The signal's Confirm a signal page.</summary>
    public static string SignalUrl(SignalLine s, string appUrl) => $"{appUrl}/strategies/{s.StrategyId}/signals/{s.SignalId}";

    static string ModeName(StrategyMode mode, string locale) =>
        mode == StrategyMode.Live ? (locale == "ca" ? "En directe" : "Live") : (locale == "ca" ? "Simulació" : "Simulation");

    /// <summary>The alert and the reminder: the signal box and the link to the signal.</summary>
    static EmailContent Signal(SignalLine s, string appUrl, string locale, string subject, string? intro, string eyebrow)
    {
        var ca = locale == "ca";
        var email = new EmailDocument(locale, appUrl).Eyebrow($"{eyebrow} · {ModeName(s.Mode, locale)}");
        if (intro is not null) email.Paragraph(intro);
        return email.Box(SignalText(s, locale))
            .Action(ca ? "Consulta'l i indica si hi has actuat:" : "See it, and say whether you acted on it:", ca ? "Veure el senyal" : "See the signal", SignalUrl(s, appUrl))
            .Disclaimer(DisclaimerFor(locale))
            .Build(subject);
    }

    /// <summary>The reminder the day after a snooze: the same text as the alert.</summary>
    public static EmailContent SnoozeReminder(SignalLine s, string appUrl, string locale = "en") => locale == "ca"
        ? Signal(s, appUrl, locale, $"Recordatori: {s.StockName}, les teves regles per a {s.StrategyName}", "Has demanat que et recordem aquest senyal.", "Recordatori")
        : Signal(s, appUrl, locale, $"Reminder: {s.StockName}, your rules for {s.StrategyName}", "You asked to be reminded of this signal.", "Reminder");

    public static EmailContent SignalAlert(SignalLine s, string appUrl, string locale = "en") => locale == "ca"
        ? Signal(s, appUrl, locale, $"{s.StockName}: es compleixen les teves regles per a {s.StrategyName}", null, s.Side == TradeSide.Sell ? "Senyal de venda" : "Senyal")
        : Signal(s, appUrl, locale, $"{s.StockName}: your rules for {s.StrategyName} are met", null, s.Side == TradeSide.Sell ? "Sale signal" : "Signal");

    /// <summary>The AI round screen of a strategy.</summary>
    public static string AiRoundUrl(Guid strategyId, string appUrl) => $"{appUrl}/strategies/{strategyId}/ai-round";

    /// <summary>"Time for the AI round of [strategy name]": one email for every round due that day.</summary>
    public static EmailContent AiRoundDue(IReadOnlyList<(Guid StrategyId, string Name)> strategies, string appUrl, string locale = "en")
    {
        var ca = locale == "ca";
        var names = strategies.Count == 1 ? strategies[0].Name
            : string.Join(", ", strategies.Take(strategies.Count - 1).Select(s => s.Name)) + (ca ? " i " : " and ") + strategies[^1].Name;
        var many = strategies.Count > 1;
        return new EmailDocument(locale, appUrl)
            .Eyebrow(ca ? "Ronda d'IA" : "AI round")
            .Paragraph(ca
                ? $"És l'hora de la ronda d'IA {(many ? "de les estratègies" : "de")} {names}. Són uns dos minuts."
                : $"Time for the AI round{(many ? "s" : "")} of {names}. It takes about two minutes.")
            .Links(strategies.Select(s => (s.Name, AiRoundUrl(s.StrategyId, appUrl))))
            .Paragraph(ca
                ? "L'aplicació no es connecta a la teva IA. Si omets una ronda, aquella ronda no té senyal."
                : "The app doesn't connect to your AI. If you skip a round, there's no signal that round.")
            .Disclaimer(DisclaimerFor(locale))
            .Build(ca ? $"És l'hora de la ronda d'IA de {names}" : $"Time for the AI round of {names}");
    }

    /// <summary>The daily overview: what waits, what changed, what is close, and one line per strategy.</summary>
    public static EmailContent Overview(DateOnly day, IReadOnlyList<SignalLine> waiting, IReadOnlyList<SignalLine> changed,
        IReadOnlyList<CloseLine> close, IReadOnlyList<ScoreLine> scoreboard, string appUrl, string locale = "en",
        IReadOnlyList<(Guid StrategyId, string Name)>? aiRoundsDue = null)
    {
        aiRoundsDue ??= [];
        var ca = locale == "ca";
        var title = ca ? $"El teu resum diari, {Date(day, locale)}" : $"Your daily overview, {Date(day)}";
        var email = new EmailDocument(locale, appUrl).Eyebrow(ca ? "Resum diari" : "Daily overview").Heading(title + ".");

        if (waiting.Count > 0 || aiRoundsDue.Count > 0)
            email.Section(ca ? "T’espera a tu" : "Waiting for you",
                aiRoundsDue.Select(r => new EmailItem(ca ? $"És l'hora de la ronda d'IA de {r.Name}." : $"Time for the AI round of {r.Name}.", AiRoundUrl(r.StrategyId, appUrl)))
                .Concat(waiting.Select(s =>
                {
                    var detail = s.Side == TradeSide.Sell ? (ca ? ": venda" : ": sale")
                        : s.StepNo is { } no && s.MaxSteps is { } max ? (ca ? $": pas {no} de {max}" : $": step {no} of {max}") : "";
                    return new EmailItem(ca ? $"{s.StockName}, {s.StrategyName}{detail}, tancament del {Date(s.Date, locale)}."
                        : $"{s.StockName}, {s.StrategyName}{detail}, close on {Date(s.Date)}.", SignalUrl(s, appUrl));
                })));

        email.Section(ca ? "Què ha canviat avui" : "What changed today", changed.Count == 0
            ? [new EmailItem(ca ? "No s'ha complert cap regla nova." : "No rules were newly met.")]
            : changed.Select(s => new EmailItem(SignalText(s, locale).Replace("\n", " "), Tag: ModeName(s.Mode, locale), Live: s.Mode == StrategyMode.Live)));

        if (close.Count > 0)
            email.Section(ca ? "A prop de la línia" : "Close to the line", close.Select(c =>
            {
                var line = c.TemplateKey switch
                {
                    "dip" => ca ? "per sobre de la zona de descompte" : "above the discount zone",
                    "bear" => ca ? "per sota de la línia de rebot" : "below the bounce line",
                    _ => ca ? "per sota de la línia de compra" : "below the buy line",
                };
                return new EmailItem($"{c.StockName}, {c.StrategyName}: {Pct(c.DistancePct)}% {line}", Tag: ModeName(c.Mode, locale), Live: c.Mode == StrategyMode.Live);
            }));

        email.Section(ca ? "Les teves estratègies" : "Your strategies", scoreboard.Select(s => new EmailItem(ca
            ? $"{s.Name} ({ModeName(s.Mode, locale)}): des del {Date(s.StartedOn, locale)}, {s.Signals} {(s.Signals == 1 ? "senyal" : "senyals")}"
            : $"{s.Name} ({ModeName(s.Mode, locale)}): since {Date(s.StartedOn)}, {s.Signals} {(s.Signals == 1 ? "signal" : "signals")}")));

        return email.Links([(ca ? "Obre l'aplicació" : "Open the app", $"{appUrl}/")])
            .Disclaimer(DisclaimerFor(locale))
            .Build(title);
    }
}
