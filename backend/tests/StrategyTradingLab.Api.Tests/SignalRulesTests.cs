using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Signals;

namespace StrategyTradingLab.Api.Tests;

public class SignalRulesTests
{
    static readonly DateOnly SignalDay = new(2026, 10, 22), Today = new(2026, 10, 26);

    /// <summary>$1.10 per euro every day.</summary>
    static decimal? Rate(DateOnly day) => 1.10m;

    static SignalTradeRequest Trade(DateOnly? date = null, decimal? price = 110, EnteredAs? enteredAs = EnteredAs.Amount, decimal? amount = 98.80m,
        decimal? shares = null, decimal? fee = 1.20m, FxSource? source = FxSource.Reference, decimal? fxRate = null) =>
        new(date ?? SignalDay.AddDays(1), price, enteredAs, amount, shares, fee, source, fxRate);

    [Fact]
    public void An_amount_works_out_the_shares_at_the_days_reference_rate()
    {
        var (trade, errors) = SignalRules.CheckTrade(Trade(), SignalDay, Today, Rate);

        Assert.Empty(errors);
        Assert.Equal(new CheckedTrade(SignalDay.AddDays(1), 110, EnteredAs.Amount, 98.80m, Math.Round(98.80m * 1.10m / 110, 6), 1.20m, 1.10m, FxSource.Reference), trade);
    }

    [Fact]
    public void Shares_work_out_the_amount_and_a_users_own_rate_is_kept()
    {
        var (trade, errors) = SignalRules.CheckTrade(Trade(enteredAs: EnteredAs.Shares, amount: null, shares: 2, fee: null, source: FxSource.User, fxRate: 1.08m),
            SignalDay, Today, _ => null);

        Assert.Empty(errors);
        Assert.Equal((EnteredAs.Shares, Math.Round(2 * 110 / 1.08m, 2), 2m, 0m, 1.08m, FxSource.User),
            (trade!.EnteredAs, trade.Amount, trade.Shares, trade.Fee, trade.FxRate, trade.FxSource));
    }

    [Theory]
    [InlineData("date", -1)] // Before the signal.
    [InlineData("date", 5)] // After today.
    public void The_date_is_from_the_signal_day_to_today(string field, int daysAfterSignal)
    {
        var (trade, errors) = SignalRules.CheckTrade(Trade(date: SignalDay.AddDays(daysAfterSignal)), SignalDay, Today, Rate);

        Assert.Null(trade);
        Assert.Contains(field, errors.Keys);
    }

    [Fact]
    public void Missing_or_wrong_numbers_are_refused_by_field()
    {
        var (_, errors) = SignalRules.CheckTrade(Trade(price: 0, amount: -5, fee: -1) with { Date = null }, SignalDay, Today, Rate);
        Assert.Equal(["amount", "date", "fee", "price"], errors.Keys.Order());

        Assert.Contains("amount", SignalRules.CheckTrade(Trade(amount: 10.005m), SignalDay, Today, Rate).Errors.Keys); // Cents only.
        Assert.Contains("shares", SignalRules.CheckTrade(Trade(enteredAs: EnteredAs.Shares, amount: null), SignalDay, Today, Rate).Errors.Keys);
        Assert.Contains("fxRate", SignalRules.CheckTrade(Trade(source: FxSource.User), SignalDay, Today, Rate).Errors.Keys);
    }

    [Fact]
    public void Without_a_reference_rate_for_the_day_the_user_is_asked_for_their_brokers()
    {
        var (trade, errors) = SignalRules.CheckTrade(Trade(), SignalDay, Today, _ => null);

        Assert.Null(trade);
        Assert.Contains("fxRate", errors.Keys);
    }

    [Theory]
    [InlineData("2026-10-22", "2026-10-23")] // Thursday → Friday.
    [InlineData("2026-10-23", "2026-10-26")] // Friday → Monday.
    [InlineData("2026-10-24", "2026-10-26")] // Saturday → Monday.
    public void The_next_weekday_skips_the_weekend(string day, string next) =>
        Assert.Equal(DateOnly.Parse(next), SignalRules.NextWeekday(DateOnly.Parse(day)));
}
