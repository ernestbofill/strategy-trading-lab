using StrategyTradingLab.Api.Backtest;
using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The backtest's data (ROADMAP item 52): the membership, the draw and the report's checks. Pure: no network, no database.</summary>
public class BacktestDataTests
{
    [Fact]
    public void The_periods_are_calendar_years_counted_back_from_today_with_a_warm_up()
    {
        var periods = BacktestPeriods.For(new DateOnly(2026, 11, 25));

        Assert.Equal(["2011–14", "2015–18", "2019–22", "2023–26"], periods.Select(p => p.Label));
        Assert.Equal(new DateOnly(2009, 10, 1), periods[0].WarmUpStart);
        Assert.Equal(new DateOnly(2026, 11, 25), periods[^1].End);
        Assert.True(periods[^1].Partial);
        Assert.False(periods[0].Partial);
    }

    [Fact]
    public void The_membership_knows_the_members_on_a_date_and_their_symbols_on_Yahoo()
    {
        var membership = Sp500Membership.Embedded;

        var members = membership.On(new DateOnly(2011, 1, 1));
        Assert.InRange(members.Count, 490, 510);
        Assert.Contains("AAPL", members);
        Assert.DoesNotContain("META", members);
        // A ticker the symbol map dates: Ingersoll-Rand plc is today's Trane; today's IR is another company.
        Assert.Equal("TT", membership.Resolve("IR", new DateOnly(2011, 1, 1)).Symbol);
        Assert.Equal("IR", membership.Resolve("IR", new DateOnly(2023, 1, 1)).Symbol);
        Assert.Equal("META", membership.Resolve("FB", new DateOnly(2015, 1, 1)).Symbol);
        Assert.Null(membership.Resolve("S", new DateOnly(2011, 1, 1)).Symbol);
        Assert.Equal(new YahooSymbol("BRK.B", "BRK-B", null), membership.Resolve("BRK.B", new DateOnly(2011, 1, 1)));
        // The second list mostly agrees; the names come from it.
        Assert.True(members.Intersect(membership.WikipediaOn(new DateOnly(2011, 1, 1))).Count() > 440);
        Assert.Equal("Sprint Nextel", membership.WikipediaName("S", new DateOnly(2011, 1, 1)));
        // Two share classes are one company, so only one can be drawn.
        Assert.Equal(membership.Company("GOOGL", new DateOnly(2015, 1, 1)), membership.Company("GOOG", new DateOnly(2015, 1, 1)));
        Assert.NotEqual(membership.Company("AAPL", new DateOnly(2015, 1, 1)), membership.Company("GOOG", new DateOnly(2015, 1, 1)));
    }

    static List<DrawCandidate> Members(int perSector, params string[] sectors) =>
        [.. sectors.SelectMany((sector, s) => Enumerable.Range(0, perSector * (s + 1))
            .Select(i => new DrawCandidate($"{sector[..3].ToUpperInvariant()}{i}", $"{sector[..3].ToUpperInvariant()}{i}", sector, 1000 + i)))];

    [Fact]
    public void The_draft_seats_each_sector_by_its_share_and_each_size_third_evenly()
    {
        // 30, 60 and 90 members: a sixth, a third and half of 180.
        var eligible = Members(30, "Energy", "Healthcare", "Technology");
        var period = BacktestPeriods.For(new DateOnly(2026, 11, 25))[0];

        var draft = DraftDraw.Draw([(period, eligible)])[0].Stocks;

        Assert.Equal(60, draft.Count);
        Assert.Equal(60, draft.Select(s => s.Symbol).Distinct().Count());
        Assert.Equal(10, draft.Count(s => s.Sector == "Energy"));
        Assert.Equal(30, draft.Count(s => s.Sector == "Technology"));
        Assert.Equal(10, draft.Count(s => s.Sector == "Technology" && s.Third == 3));
        // The same members and seed give the same draft; another seed another one.
        Assert.Equal(draft, DraftDraw.Draw([(period, eligible)])[0].Stocks);
        Assert.NotEqual(draft.Select(s => s.Symbol), DraftDraw.Draw([(period, eligible)], seed: 1)[0].Stocks.Select(s => s.Symbol));
    }

    [Fact]
    public void A_rolled_draft_keeps_the_stocks_still_in_the_index_and_replaces_the_others_from_their_sector_and_third()
    {
        var periods = BacktestPeriods.For(new DateOnly(2026, 11, 25));
        var first = Members(30, "Energy", "Healthcare", "Technology");
        var drafted = DraftDraw.Draw([(periods[0], first)])[0].Stocks;
        var leaving = drafted.Where(s => s.Sector == "Healthcare").Take(3).Select(s => s.Symbol).ToHashSet();
        var later = first.Where(c => !leaving.Contains(c.Symbol)).ToList();

        // One of the three is still a member but can't be drawn (its prices stopped): it gives up its seat too, as still in the index.
        var stillIn = leaving.Order().First();
        var rolled = DraftDraw.Draw([(periods[0], first), (periods[1], later)], members: _ => later.Select(c => c.Symbol).Append(stillIn).ToHashSet())[1];

        Assert.Equal(57, rolled.Kept.Count);
        Assert.Equal(3, rolled.Replaced.Count);
        Assert.Equal([stillIn], rolled.Replaced.Where(r => r.StillInIndex).Select(r => r.Left.Symbol));
        Assert.All(rolled.Replaced, r =>
        {
            Assert.Equal("Healthcare", r.Replacement!.Sector);
            Assert.Equal(r.Left.Third, r.Replacement.Third);
            Assert.DoesNotContain(r.Replacement.Symbol, drafted.Select(s => s.Symbol));
        });
        Assert.Equal(60, rolled.Stocks.Select(s => s.Symbol).Distinct().Count());
    }

    [Fact]
    public void The_money_weighted_rate_counts_each_euro_for_as_long_as_it_was_invested()
    {
        Assert.Equal(0.10, MoneyWeighted.AnnualRate([(new DateOnly(2021, 1, 1), -100m), (new DateOnly(2022, 1, 1), 110m)])!.Value, 6);
        // Half put in a year later earns for half the time: 100 → 115.5 at 10% on the first half and 5% on the second.
        Assert.Equal(0.10, MoneyWeighted.AnnualRate([(new DateOnly(2021, 1, 1), -50m), (new DateOnly(2022, 1, 1), -50m), (new DateOnly(2023, 1, 1), 115.5m)])!.Value, 3);
        Assert.Null(MoneyWeighted.AnnualRate([(new DateOnly(2021, 1, 1), -100m)]));
    }

    [Fact]
    public void Same_stocks_monthly_buys_the_pool_with_the_limit_and_pays_the_costs()
    {
        var period = BacktestPeriods.For(new DateOnly(2026, 11, 25))[0];
        var calendar = Enumerable.Range(0, 1461).Select(d => period.Start.AddDays(d)).Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        var money = new TestMoney();
        var days = SameStocksMonthly.BuyDays(calendar, period, money);
        var flat = PeriodPrices.From([.. calendar.Select(d => new PriceBar(d, 10, 10, 10, 10, 1))], days, period.End);

        Assert.Equal(15, days.Count);
        // A flat price loses only the fee and the spread: about 0.2% of the money, spread over the time it was held.
        Assert.InRange(SameStocksMonthly.Rate([flat, flat], days, period, money)!.Value, -0.002, -0.0003);
    }

    [Fact]
    public void The_quality_report_finds_gaps_jumps_and_prices_that_stop()
    {
        var calendar = Enumerable.Range(0, 40).Select(d => new DateOnly(2020, 1, 1).AddDays(d)).ToList();
        PriceBar Bar(int day, decimal close) => new(calendar[day], close, close, close, close, 1);
        IReadOnlyList<PriceBar> bars = [Bar(0, 10), Bar(1, 10), Bar(9, 10), Bar(10, 15), Bar(11, 15), Bar(20, 15)];

        var warnings = PriceQuality.Check("X", bars, calendar);

        Assert.Equal([PriceWarningKind.Gap, PriceWarningKind.Jump, PriceWarningKind.Gap, PriceWarningKind.Stops], warnings.Select(w => w.Kind));
        Assert.Equal(calendar[9], warnings[0].Date);
        Assert.Equal("+50% in a day", warnings[1].Detail);
        Assert.Equal(bars, PriceFile.Read(PriceFile.Write(bars)));
    }

    [Theory]
    [InlineData("C. R. Bard, Inc.", "CR Bard", true)]
    [InlineData("AT&T Inc.", "AT&T", true)]
    [InlineData("Alphabet Inc. (Class A)", "Alphabet Inc.", true)]
    [InlineData("Sprint Nextel", "SentinelOne, Inc.", false)]
    [InlineData("Qwest Communications", "Qnity Electronics, Inc.", false)]
    [InlineData("A. O. Smith", "A. O. Smith Corporation", true)]
    [InlineData("A. O. Smith", "Apple Inc.", false)]
    public void A_name_check_catches_a_ticker_reused_by_another_company(string wikipedia, string yahoo, bool match) =>
        Assert.Equal(match, BacktestData.NamesMatch(wikipedia, yahoo));
}
