using StrategyTradingLab.Api.Ai;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The rules of Your AI's top picks: when rounds are due, which scores get a step, and the prompt.</summary>
public class AiRoundRulesTests
{
    static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    static readonly Dictionary<Guid, string> Names = new() { [A] = "Alpha", [B] = "Beta", [C] = "Gamma" };
    static readonly Budget Budget = new(3000, 100, 5);

    static AiScore Score(Guid id, decimal overall) => new(id, overall, null, null, null, "");

    [Fact]
    public void The_top_score_gets_the_next_step()
    {
        var (picks, missing) = AiRoundRules.Pick([Score(A, 60), Score(B, 83), Score(C, 70)], new Dictionary<Guid, int> { [B] = 2 }, Names, Budget, 0, 1,
            new HashSet<Guid>());

        Assert.Equal([new AiPick(B, 3)], picks);
        Assert.Null(missing);
    }

    [Fact]
    public void A_stock_with_all_its_steps_passes_its_turn_to_the_next()
    {
        var (picks, _) = AiRoundRules.Pick([Score(A, 60), Score(B, 83), Score(C, 70)], new Dictionary<Guid, int> { [B] = 5 }, Names, Budget, 0, 1,
            new HashSet<Guid>());

        Assert.Equal([new AiPick(C, 1)], picks);
    }

    [Fact]
    public void A_tie_goes_to_fewer_steps_then_the_name()
    {
        var steps = new Dictionary<Guid, int> { [A] = 1 };
        var (picks, _) = AiRoundRules.Pick([Score(A, 80), Score(B, 80), Score(C, 80)], steps, Names, Budget, 0, 2, new HashSet<Guid>());

        Assert.Equal([new AiPick(B, 1), new AiPick(C, 1)], picks);
    }

    [Fact]
    public void Steps_stop_where_the_budget_does()
    {
        var (picks, missing) = AiRoundRules.Pick([Score(A, 90), Score(B, 80), Score(C, 70)], new Dictionary<Guid, int>(), Names, Budget, 2850, 3,
            new HashSet<Guid>());

        Assert.Equal([new AiPick(A, 1)], picks);
        Assert.Equal(AiNoStep.Budget, missing);
    }

    [Fact]
    public void A_stock_that_cant_signal_now_is_passed_over()
    {
        var (picks, missing) = AiRoundRules.Pick([Score(A, 90), Score(B, 80)], new Dictionary<Guid, int> { [B] = 5 }, Names, Budget, 0, 1,
            new HashSet<Guid> { A });

        Assert.Empty(picks);
        Assert.Equal(AiNoStep.Unavailable, missing);
    }

    [Fact]
    public void A_round_is_due_every_14_days_by_default_and_open_for_a_week()
    {
        var parameters = new AiParams();
        var due = new DateOnly(2026, 10, 26);

        Assert.Equal(new DateOnly(2026, 11, 9), AiRoundRules.NextDue(due, parameters));
        Assert.Equal(new DateOnly(2026, 11, 1), AiRoundRules.LastDay(due, parameters));
        // Weekly rounds close the day before the next is due.
        Assert.Equal(new DateOnly(2026, 11, 1), AiRoundRules.LastDay(due, parameters with { EveryDays = 7 }));
    }

    [Theory]
    [InlineData(1, "2026-10-27", "2026-10-26")] // Daily: open on its due day only.
    [InlineData(3, "2026-10-29", "2026-10-28")]
    [InlineData(60, "2026-12-25", "2026-11-01")]
    public void Rounds_come_every_n_days_and_close_before_the_next_is_due(int days, string next, string last)
    {
        var parameters = new AiParams { EveryDays = days };
        var due = new DateOnly(2026, 10, 26);

        Assert.Equal(DateOnly.Parse(next), AiRoundRules.NextDue(due, parameters));
        Assert.Equal(DateOnly.Parse(last), AiRoundRules.LastDay(due, parameters));
    }

    [Theory]
    [InlineData(1, "day")]
    [InlineData(5, "5 days")]
    [InlineData(7, "week")]
    [InlineData(14, "2 weeks")]
    [InlineData(20, "20 days")]
    public void The_prompt_asks_for_the_news_since_the_last_round(int days, string period)
    {
        Assert.Equal(period, AiPrompt.Period(days));
    }

    [Theory]
    [InlineData("2026-10-27T13:00:00Z", "2026-10-26")] // 09:00 in New York: the open is still to come today.
    [InlineData("2026-10-27T14:00:00Z", "2026-10-27")] // 10:00: today's open has passed, so the next one.
    [InlineData("2026-10-31T15:00:00Z", "2026-10-31")] // Saturday: Monday's open either way.
    public void The_signal_trades_at_the_first_open_after_the_round(string now, string after)
    {
        Assert.Equal(DateOnly.Parse(after), AiRoundRules.TradeAfter(DateTimeOffset.Parse(now)));
    }

    [Fact]
    public void The_prompt_lists_the_sources_on_and_the_stocks_and_nothing_about_the_user()
    {
        var prompt = AiPrompt.Build(new AiParams { Mood = false }, new DateOnly(2026, 10, 27),
            [new AiStock(A, "ZS", "Zscaler"), new AiStock(B, "VRT", "Vertiv")]);

        Assert.StartsWith("Today is October 27, 2026. Search the web for news and data from the last 2 weeks", prompt);
        Assert.Contains("- News: ", prompt);
        Assert.DoesNotContain("- Mood: ", prompt);
        Assert.Contains("| Stock | News | Figures | Overall | Reason |", prompt);
        Assert.EndsWith("Stocks:\n- ZS: Zscaler\n- VRT: Vertiv", prompt);
        Assert.Contains("Don't say which stocks to buy or sell.", prompt);
        foreach (var word in new[] { "budget", "€", "holding", "step" }) Assert.DoesNotContain(word, prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Edited_instructions_replace_the_suggested_ones_but_not_the_answer_format()
    {
        var prompt = AiPrompt.Build(new AiParams { Instructions = "Score each stock on how its AI products are doing." }, new DateOnly(2026, 10, 27),
            [new AiStock(A, "ZS", "Zscaler")]);

        Assert.Contains("Score each stock on how its AI products are doing.", prompt);
        Assert.DoesNotContain("- News: ", prompt);
        Assert.Contains("| Stock | News | Mood | Figures | Overall | Reason |", prompt);
    }

    [Fact]
    public void Scores_in_a_row_are_not_taken_for_a_phone_number()
    {
        Assert.Equal("Scores: 80 75 90 85 70 65", AiSharingScrub.Scrub("Scores: 80 75 90 85 70 65"));
        Assert.Equal("Call [number]", AiSharingScrub.Scrub("Call 600 123 456"));
    }

    [Fact]
    public void A_shared_answer_loses_emails_phone_numbers_and_link_parameters()
    {
        var scrubbed = AiSharingScrub.Scrub(
            "Contact jane.doe@example.com or +34 600 123 456. See https://news.example.com/zs?session=abc123&user=42#top on 2026-10-27, revenue 1,234,567.");

        Assert.Equal("Contact [email] or [number]. See https://news.example.com/zs on 2026-10-27, revenue 1,234,567.", scrubbed);
    }
}
