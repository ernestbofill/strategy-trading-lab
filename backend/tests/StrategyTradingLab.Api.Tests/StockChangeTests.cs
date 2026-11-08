using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Item 29: the overview's stock changes (spec: Notifications, Stock changes).</summary>
public class StockChangeTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    static (EvaluationStatus, JsonElement) Day(EvaluationStatus status, string facts = "{}") => (status, JsonDocument.Parse(facts).RootElement.Clone());

    const string FailedCandidate = """{"checks":[{"key":"trend","group":"candidate","met":false}]}""";

    [Fact]
    public void Becoming_or_stopping_being_a_candidate_is_a_change()
    {
        Assert.Equal((StockChangeKind.BecameCandidate, null), StockChanges.Between(Day(EvaluationStatus.NotCandidate), Day(EvaluationStatus.Watching, """{"distancePct":8}""")));
        Assert.Equal((StockChangeKind.NoLongerCandidate, null), StockChanges.Between(Day(EvaluationStatus.Watching), Day(EvaluationStatus.NotCandidate)));
        // Watching with a failed candidate check reads as not a candidate, as on the stock page.
        Assert.Equal((StockChangeKind.BecameCandidate, null), StockChanges.Between(Day(EvaluationStatus.Watching, FailedCandidate), Day(EvaluationStatus.Watching)));
        Assert.Equal((StockChangeKind.NoLongerCandidate, null), StockChanges.Between(Day(EvaluationStatus.Watching), Day(EvaluationStatus.SignalEnded, FailedCandidate)));
        // Becoming a candidate wins over coming close.
        Assert.Equal((StockChangeKind.BecameCandidate, null), StockChanges.Between(Day(EvaluationStatus.NotCandidate), Day(EvaluationStatus.Watching, """{"distancePct":1}""")));
    }

    [Fact]
    public void Coming_within_the_distance_is_a_change_once()
    {
        Assert.Equal((StockChangeKind.CameClose, 2.4m), StockChanges.Between(Day(EvaluationStatus.Watching, """{"distancePct":5}"""), Day(EvaluationStatus.Watching, """{"distancePct":2.4}""")));
        Assert.Equal((StockChangeKind.CameClose, 3m), StockChanges.Between(Day(EvaluationStatus.Watching, """{"distancePct":3.1}"""), Day(EvaluationStatus.Watching, """{"distancePct":3}""")));
        // Already close the day before, still far, or at the line (no distance left): nothing new.
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.Watching, """{"distancePct":2.8}"""), Day(EvaluationStatus.Watching, """{"distancePct":1.5}""")));
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.Watching, """{"distancePct":9}"""), Day(EvaluationStatus.Watching, """{"distancePct":4}""")));
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.Watching, """{"distancePct":9}"""), Day(EvaluationStatus.Watching, """{"distancePct":0}""")));
    }

    [Fact]
    public void Signals_steps_and_missing_history_are_not_changes()
    {
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.Watching), Day(EvaluationStatus.BuySignal)));
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.Building), Day(EvaluationStatus.NotCandidate)));
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.NotEnoughData), Day(EvaluationStatus.Watching)));
        Assert.Null(StockChanges.Between(Day(EvaluationStatus.NotCandidate), Day(EvaluationStatus.NotCandidate)));
    }

    [Fact]
    public void A_signal_in_between_is_skipped_to_the_last_day_that_says_whether_it_was_a_candidate()
    {
        // Newest first: a candidate, then a signal, then not a candidate.
        Assert.Equal((StockChangeKind.NoLongerCandidate, null), StockChanges.Latest([Day(EvaluationStatus.NotCandidate), Day(EvaluationStatus.BuySignal), Day(EvaluationStatus.Watching)]));
        Assert.Null(StockChanges.Latest([Day(EvaluationStatus.Watching), Day(EvaluationStatus.BuySignal), Day(EvaluationStatus.Watching)]));
        Assert.Null(StockChanges.Latest([Day(EvaluationStatus.NotCandidate), Day(EvaluationStatus.Holding)]));
        Assert.Null(StockChanges.Latest([Day(EvaluationStatus.Watching)]));
    }

    static StockChangeLine Change(StockChangeKind kind, decimal? distance = null, string template = "trend") =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Index trend", StrategyMode.Live, template, "S&P 500", kind, distance);

    [Fact]
    public void The_overview_words_the_changes_in_english_and_catalan_and_describes()
    {
        StockChangeLine[] changes = [Change(StockChangeKind.BecameCandidate), Change(StockChangeKind.NoLongerCandidate), Change(StockChangeKind.CameClose, 2.5m, "dip")];
        var (_, en, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], "https://app.example", stockChanges: changes);
        var (_, ca, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], "https://app.example", "ca", stockChanges: changes);

        Assert.Contains("Stock changes", en);
        Assert.Contains("(Live) S&P 500, Index trend: now a candidate, the rules watch it.", en);
        Assert.Contains("(Live) S&P 500, Index trend: no longer a candidate.", en);
        Assert.Contains("(Live) S&P 500, Index trend: now 2.5% above the discount zone.", en);
        Assert.Contains("Canvis de les teves accions", ca);
        Assert.Contains("S&P 500, Index trend: ara és candidata, les regles la vigilen.", ca);
        Assert.Contains("S&P 500, Index trend: ja no és candidata.", ca);
        Assert.Contains("S&P 500, Index trend: ara un 2.5% per sobre de la zona de descompte.", ca);
        Assert.DoesNotContain("buy now", en, StringComparison.OrdinalIgnoreCase);
        // Without changes the section isn't there.
        Assert.DoesNotContain("Stock changes", EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], "https://app.example").Text);
    }

    [NeonFact]
    public async Task The_summary_has_the_changes_of_every_running_strategy()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"C{tag}", Exchange = "TEST", Name = "Change asset", Currency = "USD" };
        var user = new User { Email = $"changes-{tag}@example.com", TimeZone = "Europe/Madrid", Locale = "en", EmailVerifiedAt = DateTimeOffset.UtcNow, NotifyHour = null }; // As soon as ready: the summary goes right after the check.
        db.Assets.Add(asset);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var asked = NewStrategy(user.Id, $"Asked {tag}", asset.Id);
        var quiet = NewStrategy(user.Id, $"Quiet {tag}", asset.Id);
        db.Strategies.AddRange(asked, quiet);
        foreach (var strategy in new[] { asked, quiet })
        {
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategy.Id, AssetId = asset.Id, Date = new DateOnly(2026, 10, 22), Status = EvaluationStatus.NotCandidate, Facts = JsonDocument.Parse("{}") });
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategy.Id, AssetId = asset.Id, Date = new DateOnly(2026, 10, 23), Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("""{"distancePct":6}""") });
        }
        await db.SaveChangesAsync();
        try
        {
            var sender = new CapturedSender();
            // Friday 23 October, 21:00 UTC: the session's bar is final, and it's 23:00 in Madrid.
            await ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (Email.IEmailSender)sender)
                .SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero), default);

            var body = Assert.Single(sender.Sent, m => m.To == user.Email).Body;
            // Every strategy's changes, with no opt-in; there is no Close to the line section any more.
            Assert.Contains($"Change asset, Asked {tag}: now a candidate, the rules watch it.", body);
            Assert.Contains($"Change asset, Quiet {tag}: now a candidate, the rules watch it.", body);
            Assert.DoesNotContain("Close to the line", body);
        }
        finally
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
            await db.Assets.Where(a => a.Id == asset.Id).ExecuteDeleteAsync();
        }
    }

    static Strategy NewStrategy(Guid userId, string name, Guid? assetId = null) => new()
    {
        UserId = userId, TemplateKey = "dip", Name = name, Mode = StrategyMode.Simulation, Status = StrategyStatus.Running, StartedOn = new DateOnly(2026, 10, 1),
        Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
        Assets = assetId is { } id ? [new StrategyAsset { AssetId = id, Active = true, AddedAt = DateTimeOffset.UtcNow }] : [],
    };

    sealed class CapturedSender : Email.IEmailSender
    {
        public List<(string To, string Body)> Sent { get; } = [];
        public Task SendAsync(Email.OutgoingEmail email, CancellationToken cancellationToken)
        {
            Sent.Add((email.To, email.Body));
            return Task.CompletedTask;
        }
    }
}
