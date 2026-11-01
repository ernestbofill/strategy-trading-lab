using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using StrategyTradingLab.Api.Auth;
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
    // Disclaimer
    public const string DisclaimerEn = "Not financial advice. Signals follow your rules.";
    public const string DisclaimerCa = "No és assessorament financer. Els senyals segueixen les teves regles.";
    public const string Disclaimer = DisclaimerEn;
    static readonly Message DisclaimerMsg = new(DisclaimerEn, DisclaimerCa);
    static string DisclaimerFor(string locale) => DisclaimerMsg.In(locale);

    // Modes
    static readonly Message ModeLive = new("Live", "En directe");
    static readonly Message ModeSimulation = new("Simulation", "Simulació");
    static string ModeName(StrategyMode mode, string locale) =>
        (mode == StrategyMode.Live ? ModeLive : ModeSimulation).In(locale);

    // Fact sentence entries
    static readonly Message RulesMet = new("its rules are met", "es compleixen les seves regles");
    static readonly Message SellBelowSma = new("the market closed {0}% below its 200-day average", "el mercat ha tancat un {0}% per sota de la seva mitjana de 200 dies");
    static readonly Message AiScoreTop = new("your AI scored it among the top", "la teva IA l'ha puntuat entre els primers");
    static readonly Message AiScoreTopRank = new("your AI scored it {0} out of 100, the top score of the round", "la teva IA l'ha puntuat amb {0} sobre 100, la puntuació més alta de la ronda");
    static readonly Message AiScoreRank = new("your AI scored it {0} out of 100, number {1:0} in the round", "la teva IA l'ha puntuat amb {0} sobre 100, el número {1:0} de la ronda");
    static readonly Message TrendAboveSma = new("the market closed {0}% above its 200-day average", "el mercat ha tancat un {0}% per sobre de la seva mitjana de 200 dies");
    static readonly Message BearDrop = new("down {0}% from its 6-month high and {1}% above its low", "un {0}% per sota del seu màxim de 6 mesos i un {1}% per sobre del seu mínim");
    static readonly Message DipBelowPeak = new("{0}% below its recent peak, still above its 50-day average", "{0}% per sota del seu màxim recent, encara per sobre de la mitjana de 50 dies");
    static readonly Message MilestoneSign = new("a new sign that the recovery is real: {0}", "un nou senyal que la recuperació és real: {0}");
    static readonly Message AboveBuyLine = new("{0}% above its buy line, on rising volume", "{0}% per sobre de la línia de compra, amb volum creixent");
    static readonly Message BelowBuyLine = new("{0}% below its buy line, on rising volume", "{0}% per sota de la línia de compra, amb volum creixent");

    // Signal text entries
    static readonly Message SaleTarget = new("a sale", "una venda");
    static readonly Message StepInfo = new(" Under your settings that's step {0} of {1}{2}.", " Segons la teva configuració, és el pas {0} de {1}{2}.");
    static readonly Message SignalHeadline = new("Your rules for {0} are met for {1}: {2}.", "Les teves regles per a {0} es compleixen per a {1}: {2}.");
    static readonly Message CloseOn = new("Close on {0}: {1}", "Tancament del {0}: {1}");

    // Alert and snooze entries
    static readonly Message SeeSignalPrompt = new("See it, and say whether you acted on it:", "Consulta'l i indica si hi has actuat:");
    static readonly Message SeeSignalButton = new("See the signal", "Veure el senyal");
    static readonly Message SnoozeSubject = new("Reminder: {0}, your rules for {1}", "Recordatori: {0}, les teves regles per a {1}");
    static readonly Message SnoozeIntro = new("You asked to be reminded of this signal.", "Has demanat que et recordem aquest senyal.");
    static readonly Message SnoozeEyebrow = new("Reminder", "Recordatori");
    static readonly Message AlertSubject = new("{0}: your rules for {1} are met", "{0}: es compleixen les teves regles per a {1}");
    static readonly Message SaleSignalEyebrow = new("Sale signal", "Senyal de venda");
    static readonly Message SignalEyebrow = new("Signal", "Senyal");

    // AI round due entries
    static readonly Message AndWord = new("and", "i");
    static readonly Message AiRoundEyebrow = new("AI round", "Ronda d'IA");
    static readonly Message AiRoundSubject = new("Time for the AI round of {0}", "És l'hora de la ronda d'IA de {0}");
    static readonly Message AiRoundIntroSingle = new("Time for the AI round of {0}. It takes about two minutes.", "És l'hora de la ronda d'IA de {0}. Són uns dos minuts.");
    static readonly Message AiRoundIntroMany = new("Time for the AI rounds of {0}. It takes about two minutes.", "És l'hora de la ronda d'IA de les estratègies {0}. Són uns dos minuts.");
    static readonly Message AiRoundNoConnect = new("The app doesn't connect to your AI. If you skip a round, there's no signal that round.", "L'aplicació no es connecta a la teva IA. Si omets una ronda, aquella ronda no té senyal.");

    // Overview entries
    static readonly Message OverviewTitle = new("Your daily overview, {0}", "El teu resum diari, {0}");
    static readonly Message OverviewEyebrow = new("Daily overview", "Resum diari");
    static readonly Message OverviewWaitingSection = new("Waiting for you", "T'espera a tu");
    static readonly Message OverviewAiRoundItem = new("Time for the AI round of {0}.", "És l'hora de la ronda d'IA de {0}.");
    static readonly Message OverviewSaleDetail = new(": sale", ": venda");
    static readonly Message OverviewStepDetail = new(": step {0} of {1}", ": pas {0} de {1}");
    static readonly Message OverviewWaitingSignal = new("{0}, {1}{2}, close on {3}.", "{0}, {1}{2}, tancament del {3}.");
    static readonly Message OverviewWhatChangedSection = new("What changed today", "Què ha canviat avui");
    static readonly Message OverviewNoRulesNewlyMet = new("No rules were newly met.", "No s'ha complert cap regla nova.");
    static readonly Message OverviewStockChangesSection = new("Stock changes", "Canvis de les teves accions");
    static readonly Message StockBecameCandidate = new("now a candidate, the rules watch it", "ara és candidata, les regles la vigilen");
    static readonly Message StockNoLongerCandidate = new("no longer a candidate", "ja no és candidata");
    static readonly Message StockCloseDistance = new("now {0}% {1}", "ara un {0}% {1}");
    static readonly Message OverviewCloseSection = new("Close to the line", "A prop de la línia");
    static readonly Message LineAboveDiscount = new("above the discount zone", "per sobre de la zona de descompte");
    static readonly Message LineBelowBounce = new("below the bounce line", "per sota de la línia de rebot");
    static readonly Message LineBelowBuy = new("below the buy line", "per sota de la línia de compra");
    static readonly Message OverviewYourStrategiesSection = new("Your strategies", "Les teves estratègies");
    static readonly Message OverviewStrategyScoreOne = new("{0} ({1}): since {2}, 1 signal", "{0} ({1}): des del {2}, 1 senyal");
    static readonly Message OverviewStrategyScoreMany = new("{0} ({1}): since {2}, {3} signals", "{0} ({1}): des del {2}, {3} senyals");
    static readonly Message OverviewOpenApp = new("Open the app", "Obre l'aplicació");

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
        if (s.Side == TradeSide.Sell)
        {
            var belowSma = Fact(s.Facts, "belowSmaPct");
            return belowSma is null ? RulesMet.In(locale) : SellBelowSma.In(locale, Pct(belowSma.Value));
        }
        if (s.TemplateKey == "ai")
        {
            var score = Fact(s.Facts, "score");
            var rank = Fact(s.Facts, "rank");
            if (score is null) return AiScoreTop.In(locale);
            return rank is null or 1
                ? AiScoreTopRank.In(locale, Pct(score.Value))
                : AiScoreRank.In(locale, Pct(score.Value), rank.Value);
        }
        if (s.TemplateKey == "trend")
        {
            var aboveSma = Fact(s.Facts, "aboveSmaPct");
            return aboveSma is null ? RulesMet.In(locale) : TrendAboveSma.In(locale, Pct(aboveSma.Value));
        }
        if (s.TemplateKey == "bear")
        {
            var below = Fact(s.Facts, "belowDropPeakPct") ?? Fact(s.Facts, "belowHighPct");
            var above = Fact(s.Facts, "aboveLowPct");
            return below is null || above is null ? RulesMet.In(locale) : BearDrop.In(locale, Pct(below.Value), Pct(above.Value));
        }
        if (s.TemplateKey == "dip")
        {
            var below = Fact(s.Facts, "belowPeakPct");
            return below is null ? RulesMet.In(locale) : DipBelowPeak.In(locale, Pct(below.Value));
        }
        var milestone = s.Facts.ValueKind == JsonValueKind.Object && s.Facts.TryGetProperty("milestone", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        if (milestone is not null)
        {
            var term = (locale == "ca" ? MilestoneCa(milestone) : milestone).ToLowerInvariant();
            return MilestoneSign.In(locale, term);
        }
        var aboveBuyLine = Fact(s.Facts, "aboveBuyLinePct");
        return aboveBuyLine is null ? RulesMet.In(locale)
            : (aboveBuyLine >= 0 ? AboveBuyLine : BelowBuyLine).In(locale, Pct(Math.Abs(aboveBuyLine.Value)));
    }

    /// <summary>The signal box's text: "Your rules for X are met for Y: …".</summary>
    public static string SignalText(SignalLine s, string locale = "en")
    {
        var target = s.Side == TradeSide.Sell ? SaleTarget.In(locale) : s.StockName;
        var amount = s.StepAmount is { } a ? $" ({Money(a, s.StrategyCurrency)})" : "";
        var step = s.StepNo is { } no && s.MaxSteps is { } max
            ? StepInfo.In(locale, no, max, amount)
            : "";
        var headline = SignalHeadline.In(locale, s.StrategyName, target, FactSentence(s, locale));
        var close = CloseOn.In(locale, Date(s.Date, locale), Money(s.ClosePrice, s.AssetCurrency));
        return $"{headline}{step}\n{close}";
    }

    /// <summary>The signal's Confirm a signal page.</summary>
    public static string SignalUrl(SignalLine s, string appUrl) => $"{appUrl}/strategies/{s.StrategyId}/signals/{s.SignalId}";

    /// <summary>The alert and the reminder: the signal box and the link to the signal.</summary>
    static EmailContent Signal(SignalLine s, string appUrl, string locale, string subject, string? intro, string eyebrow, string? unsubscribeToken)
    {
        var email = new EmailDocument(locale, appUrl).Eyebrow($"{eyebrow} · {ModeName(s.Mode, locale)}");
        if (intro is not null) email.Paragraph(intro);
        return email.Box(SignalText(s, locale))
            .Action(SeeSignalPrompt.In(locale), SeeSignalButton.In(locale), SignalUrl(s, appUrl))
            .WithUnsubscribe(EmailKind.Alerts, unsubscribeToken)
            .Disclaimer(DisclaimerFor(locale))
            .Build(subject);
    }

    /// <summary>The reminder the day after a snooze: the same text as the alert.</summary>
    public static EmailContent SnoozeReminder(SignalLine s, string appUrl, string locale = "en", string? unsubscribeToken = null) =>
        Signal(s, appUrl, locale,
            SnoozeSubject.In(locale, s.StockName, s.StrategyName),
            SnoozeIntro.In(locale),
            SnoozeEyebrow.In(locale),
            unsubscribeToken);

    public static EmailContent SignalAlert(SignalLine s, string appUrl, string locale = "en", string? unsubscribeToken = null) =>
        Signal(s, appUrl, locale,
            AlertSubject.In(locale, s.StockName, s.StrategyName),
            null,
            (s.Side == TradeSide.Sell ? SaleSignalEyebrow : SignalEyebrow).In(locale),
            unsubscribeToken);

    /// <summary>The AI round screen of a strategy.</summary>
    public static string AiRoundUrl(Guid strategyId, string appUrl) => $"{appUrl}/strategies/{strategyId}/ai-round";

    /// <summary>"Time for the AI round of [strategy name]": one email for every round due that day.</summary>
    public static EmailContent AiRoundDue(IReadOnlyList<(Guid StrategyId, string Name)> strategies, string appUrl, string locale = "en", string? unsubscribeToken = null)
    {
        var names = strategies.Count == 1 ? strategies[0].Name
            : $"{string.Join(", ", strategies.Take(strategies.Count - 1).Select(s => s.Name))} {AndWord.In(locale)} {strategies[^1].Name}";
        var intro = (strategies.Count > 1 ? AiRoundIntroMany : AiRoundIntroSingle).In(locale, names);
        return new EmailDocument(locale, appUrl)
            .Eyebrow(AiRoundEyebrow.In(locale))
            .Paragraph(intro)
            .Links(strategies.Select(s => (s.Name, AiRoundUrl(s.StrategyId, appUrl))))
            .Paragraph(AiRoundNoConnect.In(locale))
            .WithUnsubscribe(EmailKind.Alerts, unsubscribeToken)
            .Disclaimer(DisclaimerFor(locale))
            .Build(AiRoundSubject.In(locale, names));
    }

    /// <summary>What a template's distance measures to: "below the buy line".</summary>
    static string LineName(string templateKey, string locale) =>
        (templateKey switch
        {
            "dip" => LineAboveDiscount,
            "bear" => LineBelowBounce,
            _ => LineBelowBuy,
        }).In(locale);

    /// <summary>The daily overview: what waits, what changed, the stock changes asked for, what is close, and one line per strategy.</summary>
    public static EmailContent Overview(DateOnly day, IReadOnlyList<SignalLine> waiting, IReadOnlyList<SignalLine> changed,
        IReadOnlyList<CloseLine> close, IReadOnlyList<ScoreLine> scoreboard, string appUrl, string locale = "en",
        IReadOnlyList<(Guid StrategyId, string Name)>? aiRoundsDue = null, string? unsubscribeToken = null, IReadOnlyList<StockChangeLine>? stockChanges = null)
    {
        aiRoundsDue ??= [];
        stockChanges ??= [];
        var title = OverviewTitle.In(locale, Date(day, locale));
        var email = new EmailDocument(locale, appUrl).Eyebrow(OverviewEyebrow.In(locale)).Heading(title + ".");

        if (waiting.Count > 0 || aiRoundsDue.Count > 0)
            email.Section(OverviewWaitingSection.In(locale),
                aiRoundsDue.Select(r => new EmailItem(OverviewAiRoundItem.In(locale, r.Name), AiRoundUrl(r.StrategyId, appUrl)))
                .Concat(waiting.Select(s =>
                {
                    var detail = s.Side == TradeSide.Sell ? OverviewSaleDetail.In(locale)
                        : s.StepNo is { } no && s.MaxSteps is { } max ? OverviewStepDetail.In(locale, no, max) : "";
                    return new EmailItem(OverviewWaitingSignal.In(locale, s.StockName, s.StrategyName, detail, Date(s.Date, locale)), SignalUrl(s, appUrl));
                })));

        email.Section(OverviewWhatChangedSection.In(locale), changed.Count == 0
            ? [new EmailItem(OverviewNoRulesNewlyMet.In(locale))]
            : changed.Select(s => new EmailItem(SignalText(s, locale).Replace("\n", " "), Tag: ModeName(s.Mode, locale), Live: s.Mode == StrategyMode.Live)));

        if (stockChanges.Count > 0)
            email.Section(OverviewStockChangesSection.In(locale), stockChanges.Select(c =>
            {
                var change = c.Kind switch
                {
                    StockChangeKind.BecameCandidate => StockBecameCandidate.In(locale),
                    StockChangeKind.NoLongerCandidate => StockNoLongerCandidate.In(locale),
                    _ => StockCloseDistance.In(locale, Pct(c.DistancePct ?? 0), LineName(c.TemplateKey, locale)),
                };
                return new EmailItem($"{c.StockName}, {c.StrategyName}: {change}.", Tag: ModeName(c.Mode, locale), Live: c.Mode == StrategyMode.Live);
            }));

        if (close.Count > 0)
            email.Section(OverviewCloseSection.In(locale), close.Select(c =>
                new EmailItem($"{c.StockName}, {c.StrategyName}: {Pct(c.DistancePct)}% {LineName(c.TemplateKey, locale)}", Tag: ModeName(c.Mode, locale), Live: c.Mode == StrategyMode.Live)));

        email.Section(OverviewYourStrategiesSection.In(locale), scoreboard.Select(s => new EmailItem(
            (s.Signals == 1 ? OverviewStrategyScoreOne : OverviewStrategyScoreMany)
                .In(locale, s.Name, ModeName(s.Mode, locale), Date(s.StartedOn, locale), s.Signals))));

        return email.Links([(OverviewOpenApp.In(locale), $"{appUrl}/")])
            .WithUnsubscribe(EmailKind.Overview, unsubscribeToken)
            .Disclaimer(DisclaimerFor(locale))
            .Build(title);
    }
}
