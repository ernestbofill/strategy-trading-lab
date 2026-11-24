using System.Globalization;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Index trend (POC <c>index_trend</c>). All in above the <c>SmaDays</c> average plus <c>BandPct</c>,
/// all out below the average minus <c>BandPct</c>; inside the band the trend stays what it was. The buy only up to
/// <c>MaxChasePct</c> above the buy line (Ernest, 2026-11-24): further above is too late to chase, so it waits for a pullback. Keeps state between days
/// (whether the trend is up and when it last broke down): one instance per run.
/// </summary>
public class TrendRules : BuyRulesBase
{
    readonly TrendParams p;
    readonly IReadOnlyList<decimal> close;
    readonly decimal?[] sma;

    bool up;
    DateOnly? downSince;

    public TrendRules(IReadOnlyList<PriceBar> bars, TrendParams parameters) : base(bars.Count)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        sma = Indicators.Sma(close, p.SmaDays);
    }

    public override int MinHistory => p.SmaDays;

    public override int CooldownDays => p.CooldownDays;

    protected override RuleDecision DecideDay(int i, DayContext day)
    {
        if (sma[i] is not { } slow)
            return new RuleDecision(RuleStatus.NotEnoughData, $"Waiting for {p.SmaDays} trading days of history.", new Dictionary<string, object>());

        var price = close[i];
        var buyLine = slow * (1 + p.BandPct / 100);
        var sellLine = slow * (1 - p.BandPct / 100);
        decimal? chaseLimit = p.MaxChasePct is { } chase ? slow * (1 + (p.BandPct + chase) / 100) : null;
        if (price > buyLine)
        {
            up = true;
        }
        else if (price < sellLine)
        {
            if (up || downSince is null) downSince = day.Day;
            up = false;
        }

        var aboveBuy = price > buyLine;
        var tooLate = price > chaseLimit;
        var inWindow = aboveBuy && !tooLate;
        var waiting = day.Cooling || day.ActedNow;
        RuleStatus status;
        string? reason = null;
        if (!day.HasPosition)
        {
            if (inWindow && !waiting)
            {
                status = RuleStatus.BuySignal;
            }
            else if (!inWindow && day.Previous == RuleStatus.BuySignal && !waiting)
            {
                status = RuleStatus.SignalEnded;
                reason = tooLate
                    ? string.Create(CultureInfo.InvariantCulture,
                        $"Price is more than {p.BandPct + p.MaxChasePct:0.##}% above the {p.SmaDays}-day average: too late to chase.")
                    : up ? "Price fell back below the buy line." : "The trend turned down before buying.";
            }
            else
            {
                status = RuleStatus.Watching;
            }
        }
        else
        {
            if (!up)
            {
                status = RuleStatus.SellSignal;
                if (price < sellLine)
                {
                    reason = string.Create(CultureInfo.InvariantCulture,
                        $"Trend broke: closed {p.BandPct:0.##}% or more below its {p.SmaDays}-day average ({slow:N2}).");
                }
                else if (downSince is { } ds)
                {
                    reason = string.Create(CultureInfo.InvariantCulture,
                        $"Trend turned down on {ds:yyyy-MM-dd} (closed {p.BandPct:0.##}% or more below the {p.SmaDays}-day average); today it's inside the ±{p.BandPct:0.##}% band ({slow:N2}), not back above +{p.BandPct:0.##}%.");
                }
                else
                {
                    reason = string.Create(CultureInfo.InvariantCulture,
                        $"Price is not in a confirmed uptrend (need +{p.BandPct:0.##}% above the {p.SmaDays}-day average, {slow:N2}).");
                }
            }
            else
            {
                status = RuleStatus.Holding;
            }
        }

        var vsSmaPct = Indicators.PctFrom(price, slow);
        var facts = new Dictionary<string, object>
        {
            ["aboveSmaPct"] = Math.Round(vsSmaPct, 2),
            ["belowSmaPct"] = Math.Round(Math.Max(0, -vsSmaPct), 2),
            ["sma"] = Math.Round(slow, 4),
            ["buyLine"] = Math.Round(buyLine, 4),
            ["sellLine"] = Math.Round(sellLine, 4),
            // How far the price still has to rise to reach the buy line (0 at or above it): "close to the line".
            ["distancePct"] = Math.Round(Math.Max(0, Indicators.PctFrom(buyLine, price)), 2),
        };
        // Out of the market: the buy line. In it: the sell line (inside the band the position stays held).
        var vsSma = Math.Round(vsSmaPct, 1);
        List<RuleCheck> checks = day.HasPosition
            ? [new("sellLine", CheckGroup.Sell, !up, Math.Round(Indicators.PctFrom(price, sellLine), 1), p.BandPct,
                Level: Math.Round(sellLine, 2), MidLevel: Math.Round(slow, 2), EndLevel: Math.Round(buyLine, 2), SubValue: vsSma)]
            : [new("buyLine", CheckGroup.Buy, aboveBuy, Math.Round(Indicators.PctFrom(price, buyLine), 1), p.BandPct,
                Level: Math.Round(buyLine, 2), MidLevel: Math.Round(slow, 2), EndLevel: Math.Round(sellLine, 2), SubValue: vsSma,
                Progress: RuleCheck.ProgressOf(price, buyLine))];
        // The buy window's top: past it the stock is too late to chase (StockStates.PastBuyRule).
        if (!day.HasPosition && chaseLimit is { } limit)
            checks.Add(new("chase", CheckGroup.Buy, !tooLate, Math.Round(vsSmaPct, 1), p.BandPct, p.BandPct + p.MaxChasePct,
                Level: Math.Round(buyLine, 2), MidLevel: Math.Round(slow, 2), EndLevel: Math.Round(limit, 2)));
        // After a sale the cooldown holds the next buy back.
        if (!day.HasPosition && RuleCheck.Cooldown(day, p.CooldownDays) is { } cooldown) checks.Add(cooldown);
        return new RuleDecision(status, reason, facts, WindowOpen: inWindow) { Checks = checks };
    }
}
