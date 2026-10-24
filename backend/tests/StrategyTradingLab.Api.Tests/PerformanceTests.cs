using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Strategies;

namespace StrategyTradingLab.Api.Tests;

public class PerformanceTests
{
    static readonly Guid Stock = Guid.NewGuid(), Index = Guid.NewGuid();
    static readonly DateOnly Mon = new(2026, 10, 5), Tue = Mon.AddDays(1), Wed = Mon.AddDays(2), Thu = Mon.AddDays(3);

    /// <summary>A USD stock and a USD index fund; a dollar is 0.8 euro until Tuesday, then 0.5.</summary>
    static PriceBook Book() => new(
        new Dictionary<Guid, (string, IReadOnlyList<DayPrice>)>
        {
            [Stock] = ("USD", [new(Mon, 10, 10), new(Tue, 10, 12), new(Wed, 12, 15)]),
            [Index] = ("USD", [new(Tue, 100, 100), new(Wed, 100, 110)]),
        },
        new ExchangeRates(new() { ["USD"] = [(Mon, 1.25m), (Tue, 2m)] }));

    [Fact]
    public void The_gain_values_the_shares_at_the_days_close_and_rate_against_their_cost_with_fees()
    {
        // €100 + €1 fee buys 12.5 shares at $10 on Monday ($1.25 per euro).
        Buy[] buys = [new(Stock, Mon, 12.5m, 100, 1)];

        Assert.Equal(Math.Round((12.5m * 10 / 1.25m - 101) / 101 * 100, 1), Performance.GainPct(buys, Book(), Mon, "EUR"));
        // Wednesday: $15 a share at $2 per euro is €93.75.
        Assert.Equal(Math.Round((93.75m - 101) / 101 * 100, 1), Performance.GainPct(buys, Book(), Wed, "EUR"));
        // Thursday has no price: Wednesday's close and the latest rate count.
        Assert.Equal(Performance.GainPct(buys, Book(), Wed, "EUR"), Performance.GainPct(buys, Book(), Thu, "EUR"));
        Assert.Null(Performance.GainPct(buys, Book(), Mon.AddDays(-1), "EUR")); // Before the buy.
        Assert.Null(Performance.GainPct(buys, Book(), Mon, "GBP")); // No pound rate.
        Assert.Null(Performance.GainPct([new Buy(Guid.NewGuid(), Mon, 1, 10, 0)], Book(), Mon, "EUR")); // No prices.
        // A buy that can't be valued is left out, cost and all, instead of hiding the others.
        Assert.Equal(Performance.GainPct(buys, Book(), Wed, "EUR"),
            Performance.GainPct([.. buys, new Buy(Guid.NewGuid(), Mon, 1, 10, 0)], Book(), Wed, "EUR"));
    }

    [Fact]
    public void The_index_gets_the_same_money_on_the_same_day_or_the_next_one_it_trades_or_the_last_close_before_it()
    {
        Buy[] buys = [new(Stock, Mon, 12.5m, 100, 1), new(Stock, Wed, 4, 50, 0)];

        var index = Performance.IndexBuys(buys, Book(), Index, "EUR")!;

        // Monday's €101 buys on Tuesday, the fund's first day: $202 at $100.
        Assert.Equal(new Buy(Index, Tue, 2.02m, 101, 0), index[0]);
        Assert.Equal(new Buy(Index, Wed, 1m, 50, 0), index[1]);
        // Thursday isn't stored yet: Wednesday's close, $110, counted from Thursday.
        Assert.Equal(new Buy(Index, Thu, 10m * 2 / 110, 10, 0), Performance.IndexBuys([new Buy(Stock, Thu, 1, 10, 0)], Book(), Index, "EUR")![0]);
        Assert.Null(Performance.IndexBuys(buys, Book(), Guid.NewGuid(), "EUR")); // A fund with no prices.
    }

    [Fact]
    public void The_history_has_a_point_per_trading_day_from_the_first_buy_and_is_thinned_to_the_limit()
    {
        Buy[] you = [new(Stock, Tue, 10, 5, 0)];

        var history = Performance.History(you, null, Performance.IndexBuys(you, Book(), Index, "EUR"), Book(), Thu, "EUR");

        Assert.Equal([Tue, Wed], history.Select(p => p.Date));
        Assert.Equal(Performance.GainPct(you, Book(), Wed, "EUR"), history[1].You);
        Assert.Null(history[1].Rules);
        Assert.Equal(10m, history[1].Index); // $100 to $110.
        Assert.Empty(Performance.History([], null, null, Book(), Thu, "EUR"));

        var thinned = Performance.Thin(Enumerable.Range(0, 1000).ToList(), 120);
        Assert.Equal(120, thinned.Count);
        Assert.Equal((0, 999), (thinned[0], thinned[^1]));
    }

    [Theory]
    [InlineData(StrategyMode.Live, EvaluationStatus.BuySignal, true, true, 1, StockStatus.WaitingForYou)]
    [InlineData(StrategyMode.Live, EvaluationStatus.BuySignal, false, true, 1, StockStatus.Building)] // Answered.
    [InlineData(StrategyMode.Simulation, EvaluationStatus.BuySignal, false, true, 1, StockStatus.Signal)]
    [InlineData(StrategyMode.Simulation, EvaluationStatus.BuySignal, false, false, 0, StockStatus.Watching)] // No budget left for it.
    [InlineData(StrategyMode.Simulation, EvaluationStatus.Building, false, false, 5, StockStatus.Holding)]
    [InlineData(StrategyMode.Simulation, EvaluationStatus.Holding, false, false, 2, StockStatus.Holding)] // Comeback milestones used.
    [InlineData(StrategyMode.Simulation, EvaluationStatus.Watching, false, false, 2, StockStatus.Building)]
    [InlineData(StrategyMode.Simulation, EvaluationStatus.SignalEnded, false, false, 0, StockStatus.Watching)]
    [InlineData(StrategyMode.Live, EvaluationStatus.NotCandidate, false, false, 0, StockStatus.NotCandidate)]
    [InlineData(StrategyMode.Live, EvaluationStatus.NotEnoughData, false, false, 0, StockStatus.NotEnoughData)]
    [InlineData(StrategyMode.Live, null, false, false, 0, StockStatus.NotChecked)]
    public void A_stocks_pill_follows_the_waiting_signal_then_the_steps_then_the_rules(StrategyMode mode, EvaluationStatus? latest,
        bool waiting, bool todaysSignal, int rulesSteps, StockStatus expected) =>
        Assert.Equal(expected, StrategyPage.Status(mode, latest, waiting, todaysSignal, rulesSteps, maxSteps: 5));
}
