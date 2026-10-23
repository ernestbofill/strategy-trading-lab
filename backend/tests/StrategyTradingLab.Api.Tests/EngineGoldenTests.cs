using System.Text.Json;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The engine against the POC's own output on real prices (Golden/README.md): same status, step and signals, every day.</summary>
public class EngineGoldenTests
{
    record Golden(string Symbol, string[] Dates, decimal[] Close, long[]? Volume, string[] Status, int[] Step, string?[] Reason, int[] NewBuy);

    static Golden Load(string name) =>
        JsonSerializer.Deserialize<Golden>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", $"{name}.json")), JsonSerializerOptions.Web)!;

    static IReadOnlyList<PriceBar> Bars(Golden g) =>
        [.. g.Dates.Select((d, i) => new PriceBar(DateOnly.Parse(d), g.Close[i], g.Close[i], g.Close[i], g.Close[i], g.Volume?[i] ?? 0))];

    /// <summary>The POC's status words ("not_enough_data") as the engine's names.</summary>
    static RuleStatus Status(string word) => Enum.Parse<RuleStatus>(word.Replace("_", ""), ignoreCase: true);

    static void Matches(Golden g, IReadOnlyList<DayEvaluation> days)
    {
        Assert.Equal(g.Dates.Length, days.Count);
        for (var i = 0; i < days.Count; i++)
        {
            Assert.True(Status(g.Status[i]) == days[i].Status, $"{g.Symbol} {g.Dates[i]}: expected {g.Status[i]}, got {days[i].Status}");
            Assert.True(g.Step[i] == (days[i].StepNo ?? 0), $"{g.Symbol} {g.Dates[i]}: expected step {g.Step[i]}, got {days[i].StepNo}");
            Assert.True(g.Reason[i] == days[i].Reason, $"{g.Symbol} {g.Dates[i]}: expected reason '{g.Reason[i]}', got '{days[i].Reason}'");
        }
        Assert.Equal(g.NewBuy, days.Select((d, i) => (d, i)).Where(x => x.d.NewBuy).Select(x => x.i));
    }

    [Theory]
    [InlineData("dip-ETN-a")]
    [InlineData("dip-ETN-b")]
    [InlineData("dip-MPWR")]
    [InlineData("dip-NOW")]
    [InlineData("dip-CRM")]
    public void Dip_in_winners_matches_the_POC_day_by_day(string name)
    {
        var golden = Load(name);
        var bars = Bars(golden);

        var days = RulesRunner.Run(new DipRules(bars, new DipParams { CooldownDays = 10 }), bars, new StepPlan(5));

        Matches(golden, days);
    }

    [Theory]
    [InlineData("comeback-MOD-800")]
    [InlineData("comeback-NOW-2400")]
    [InlineData("comeback-SMCI-800")]
    [InlineData("comeback-TSLA-0")]
    [InlineData("comeback-VICR-1600")]
    [InlineData("comeback-VRT-800")]
    public void The_comeback_matches_the_POC_day_by_day(string name)
    {
        var golden = Load(name);
        var bars = Bars(golden);

        var days = RulesRunner.Run(new ComebackRules(bars, new ComebackParams()), bars, new StepPlan(5));

        Matches(golden, days);
    }
}
