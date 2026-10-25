using System.Globalization;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Ride the long trend (POC Index trend, <c>index_trend</c>). All in above the <c>SmaDays</c> average plus <c>BandPct</c>,
/// all out below the average minus <c>BandPct</c>; inside the band the trend stays what it was. Keeps state between days
/// (whether the trend is up and when it last broke down): one instance per run.
/// </summary>
public class TrendRules : IBuyRules
{
    readonly TrendParams p;
    readonly IReadOnlyList<decimal> close;
    readonly decimal?[] sma;

    bool up;
    DateOnly? downSince;

    public TrendRules(IReadOnlyList<PriceBar> bars, TrendParams parameters)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        sma = Indicators.Sma(close, p.SmaDays);
    }

    public int Length => close.Count;

    public int MinHistory => p.SmaDays;

    public int CooldownDays => p.CooldownDays;

    int lastDecided = -1;

    public RuleDecision Decide(int i, DayContext day)
    {
        if (i <= lastDecided) throw new InvalidOperationException("These rules keep state from day to day and were already run: build new ones for each run.");
        lastDecided = i;
        if (sma[i] is not { } slow)
            return new RuleDecision(RuleStatus.NotEnoughData, $"Waiting for {p.SmaDays} trading days of history.", new Dictionary<string, object>());

        var price = close[i];
        var buyLine = slow * (1 + p.BandPct / 100);
        var sellLine = slow * (1 - p.BandPct / 100);
        if (price > buyLine)
        {
            up = true;
        }
        else if (price < sellLine)
        {
            if (up || downSince is null) downSince = day.Day;
            up = false;
        }

        var waiting = day.Cooling || day.ActedNow;
        RuleStatus status;
        string? reason = null;
        if (!day.HasPosition)
        {
            if (up && !waiting)
            {
                status = RuleStatus.BuySignal;
            }
            else if (!up && day.Previous == RuleStatus.BuySignal && !waiting)
            {
                status = RuleStatus.SignalEnded;
                reason = "The trend turned down before buying.";
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
        return new RuleDecision(status, reason, facts, WindowOpen: up);
    }
}
