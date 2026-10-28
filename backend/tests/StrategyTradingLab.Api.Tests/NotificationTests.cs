using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class NotificationTests
{
    static SignalLine Dip(string facts = """{"belowPeakPct":8.2}""") => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), "Zscaler & friends", StrategyMode.Live, "dip", "Zscaler", "USD", "EUR",
        new DateOnly(2026, 10, 22), 187.5m, 2, 5, 100, JsonDocument.Parse(facts).RootElement);

    [Fact]
    public void A_dip_alert_says_what_the_rules_found_with_the_step_and_the_close()
    {
        var (subject, body, _) = EmailTexts.SignalAlert(Dip(), "https://app.example");

        Assert.Equal("Zscaler: your rules for Zscaler & friends are met", subject);
        Assert.Contains("Your rules for Zscaler & friends are met for Zscaler: 8.2% below its recent peak, still above its 50-day average.", body);
        Assert.Contains("Under your settings that's step 2 of 5 (€100).", body);
        Assert.Contains("Close on 22 Oct 2026: $187.5", body);
        Assert.Contains("https://app.example/strategies/11111111-1111-1111-1111-111111111111/signals/22222222-2222-2222-2222-222222222222", body);
        Assert.EndsWith("Not financial advice. Signals follow your rules.", body);
    }

    [Fact]
    public void A_comeback_alert_names_the_milestone_for_later_steps_and_the_line_for_the_first()
    {
        var first = Dip("""{"aboveBuyLinePct":2.4}""") with { TemplateKey = "comeback" };
        var later = Dip("""{"milestone":"Golden cross (SMA 50 above SMA 200)"}""") with { TemplateKey = "comeback" };

        Assert.Contains("2.4% above its buy line, on rising volume", EmailTexts.SignalText(first));
        Assert.Contains("a new sign that the recovery is real: golden cross (sma 50 above sma 200)", EmailTexts.SignalText(later));
    }

    [Fact]
    public void Bear_and_trend_alerts_describe_drops_trend_buys_and_trend_sales()
    {
        var bear = Dip("""{"belowHighPct":16.5,"aboveLowPct":5.2}""") with { TemplateKey = "bear", StockName = "S&P 500" };
        var trendBuy = Dip("""{"aboveSmaPct":3.4}""") with { TemplateKey = "trend", StockName = "S&P 500", StepNo = null, MaxSteps = null, StepAmount = 1000 };
        var trendSell = Dip("""{"belowSmaPct":3.1}""") with { TemplateKey = "trend", StockName = "S&P 500", StepNo = null, MaxSteps = null, StepAmount = null, Side = TradeSide.Sell };

        Assert.Contains("down 16.5% from its 6-month high and 5.2% above its low", EmailTexts.SignalText(bear));
        Assert.Contains("the market closed 3.4% above its 200-day average", EmailTexts.SignalText(trendBuy));
        var (_, sellBody, _) = EmailTexts.SignalAlert(trendSell, "https://app.example");
        Assert.Contains("Your rules for Zscaler & friends are met for a sale: the market closed 3.1% below its 200-day average.", sellBody);
        Assert.DoesNotContain("Under your settings", sellBody);
    }

    [Fact]
    public void The_overview_has_its_sections_describes_and_never_instructs()
    {
        var waiting = new[] { Dip() };
        var close = new[]
        {
            new CloseLine("Zscaler & friends", StrategyMode.Live, "dip", "Palantir", 3.2m),
            new CloseLine("The comeback", StrategyMode.Simulation, "comeback", "Intel", 4.5m),
            new CloseLine("Bear markets", StrategyMode.Live, "bear", "S&P 500", 1.8m),
            new CloseLine("Index trend", StrategyMode.Live, "trend", "S&P 500", 1.2m),
        };
        var score = new[] { new ScoreLine("Zscaler & friends", StrategyMode.Live, new DateOnly(2026, 10, 1), 1), new ScoreLine("The comeback", StrategyMode.Simulation, new DateOnly(2026, 10, 5), 0) };

        var (subject, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), waiting, [], close, score, "https://app.example");

        Assert.Equal("Your daily overview, 23 Oct 2026", subject);
        foreach (var section in new[] { "Waiting for you", "What changed today", "Close to the line", "Your strategies" }) Assert.Contains(section, body);
        Assert.Contains("- No rules were newly met.", body);
        Assert.Contains("(Live) Palantir, Zscaler & friends: 3.2% above the discount zone", body);
        Assert.Contains("(Simulation) Intel, The comeback: 4.5% below the buy line", body);
        Assert.Contains("(Live) S&P 500, Bear markets: 1.8% below the bounce line", body);
        Assert.Contains("(Live) S&P 500, Index trend: 1.2% below the buy line", body);
        Assert.Contains("The comeback (Simulation): since 5 Oct 2026, 0 signals", body);
        Assert.EndsWith("Not financial advice. Signals follow your rules.", body);
        Assert.DoesNotContain("buy now", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_empty_waiting_list_leaves_that_section_out()
    {
        var (_, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], [], "https://app.example");

        Assert.DoesNotContain("Waiting for you", body);
    }

    [Fact]
    public void The_ai_round_email_names_every_round_due_that_day()
    {
        var (subject, body, _) = EmailTexts.AiRoundDue(
            [(Guid.Parse("11111111-1111-1111-1111-111111111111"), "My AI picks"), (Guid.Parse("33333333-3333-3333-3333-333333333333"), "Tech scores")],
            "https://app.example");

        Assert.Equal("Time for the AI round of My AI picks and Tech scores", subject);
        Assert.StartsWith("Time for the AI rounds of My AI picks and Tech scores. It takes about two minutes.", body);
        Assert.Contains("My AI picks: https://app.example/strategies/11111111-1111-1111-1111-111111111111/ai-round", body);
        Assert.EndsWith("Not financial advice. Signals follow your rules.", body);
    }

    [Fact]
    public void The_ai_round_email_and_signal_are_in_catalan_for_a_catalan_user()
    {
        var (subject, body, _) = EmailTexts.AiRoundDue([(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Les meves IA")], "https://app.example", "ca");
        var signal = Dip("""{"score":91,"rank":1}""") with { TemplateKey = "ai" };

        Assert.Equal("És l'hora de la ronda d'IA de Les meves IA", subject);
        Assert.EndsWith(EmailTexts.DisclaimerCa, body);
        Assert.Equal("la teva IA l'ha puntuat amb 91 sobre 100, la puntuació més alta de la ronda", EmailTexts.FactSentence(signal, "ca"));
        Assert.Equal("your AI scored it 91 out of 100, the top score of the round", EmailTexts.FactSentence(signal));
    }

    [Fact]
    public void The_overview_lists_ai_rounds_due_with_the_signals_waiting()
    {
        var (_, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], [], "https://app.example", "en",
            [(Guid.Parse("11111111-1111-1111-1111-111111111111"), "My AI picks")]);

        Assert.Contains("Waiting for you\n- Time for the AI round of My AI picks. https://app.example/strategies/11111111-1111-1111-1111-111111111111/ai-round", body);
    }

    sealed class Captured : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public Task<string> SendToAccountAsync(EmailContent email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
        {
            Sent.Add((email.To, email.Subject, email.Body));
            return Task.CompletedTask;
        }
    }

    sealed class NotSetUpThenWorks : IEmailSender
    {
        public bool SetUp { get; set; }
        public int Sent { get; private set; }
        public Task<string> SendToAccountAsync(EmailContent email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
        {
            if (!SetUp) throw new EmailNotSetUpException();
            Sent++;
            return Task.CompletedTask;
        }
    }

    static async Task<(Guid UserId, Guid StrategyId, Guid AssetId, Guid SignalId, string Email)> SeedAsync(AppDbContext db, bool verified = true, bool overview = true,
        StrategyMode mode = StrategyMode.Live, string timeZone = "Europe/Madrid", bool evaluated = true)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"N{tag}", Exchange = "TEST", Name = "Test asset", Currency = "USD" };
        var user = new User { Email = $"notify-{tag}@example.com", TimeZone = timeZone, Locale = "en", OverviewEmail = overview, EmailVerifiedAt = verified ? DateTimeOffset.UtcNow : null };
        db.Assets.Add(asset);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Notify {tag}", Mode = mode, Status = StrategyStatus.Running, StartedOn = new DateOnly(2026, 10, 1),
            Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = DateTimeOffset.UtcNow }],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        var signal = new Signal
        {
            StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 5, StepAmount = 100, SignalDate = new DateOnly(2026, 10, 22),
            ClosePrice = 187.5m, Facts = JsonDocument.Parse("""{"belowPeakPct":8.2}"""), Status = mode == StrategyMode.Live ? SignalStatus.Waiting : SignalStatus.Confirmed,
            AnsweredAt = mode == StrategyMode.Live ? null : DateTimeOffset.UtcNow,
        };
        db.Signals.Add(signal);
        // The daily check has evaluated the sessions the overview tests ask about (Thursday 22 and Friday 23 October).
        if (evaluated)
            foreach (var day in new[] { new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 23) })
                db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategy.Id, AssetId = asset.Id, Date = day, Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("{\"distancePct\":3.2}") });
        await db.SaveChangesAsync();
        return (user.Id, strategy.Id, asset.Id, signal.Id, user.Email);
    }

    static async Task CleanAsync(AppDbContext db, Guid userId, Guid assetId)
    {
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        await db.Assets.Where(a => a.Id == assetId).ExecuteDeleteAsync();
    }

    static NotificationService Service(IServiceScope scope, Captured sender) =>
        ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (IEmailSender)sender);

    [NeonFact]
    public async Task A_live_signal_is_alerted_once_and_recorded()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, _, assetId, signalId, email) = await SeedAsync(db);
        try
        {
            var sender = new Captured();
            var first = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            var second = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);

            Assert.Equal((1, 0), (first, second));
            Assert.Equal(email, Assert.Single(sender.Sent).To);
            var row = await db.Notifications.SingleAsync(n => n.UserId == userId);
            Assert.Equal((NotificationKind.Signal, NotificationChannel.Email, new DateOnly(2026, 10, 22)), (row.Kind, row.Channel, row.ForDate));
            Assert.NotNull(row.SentAt);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task No_alert_for_an_unverified_address_a_simulation_or_a_strategy_with_notifications_off()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unverified = await SeedAsync(db, verified: false);
        var simulation = await SeedAsync(db, mode: StrategyMode.Simulation);
        var off = await SeedAsync(db);
        try
        {
            await db.Strategies.Where(s => s.Id == off.StrategyId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Notifications, false));
            var sender = new Captured();

            var sent = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);

            Assert.Equal(0, sent);
            Assert.Empty(sender.Sent);
        }
        finally
        {
            foreach (var seeded in new[] { unverified, simulation, off }) await CleanAsync(db, seeded.UserId, seeded.AssetId);
        }
    }

    [NeonFact]
    public async Task The_overview_goes_out_once_a_session_right_after_the_check_but_not_in_the_users_night()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wanted = await SeedAsync(db);
        var off = await SeedAsync(db, overview: false);
        var unverified = await SeedAsync(db, verified: false);
        var tokyo = await SeedAsync(db, timeZone: "Asia/Tokyo");
        try
        {
            var sender = new Captured();
            // New York is UTC-4 on 2026-10-22: the bar is final at 20:30 UTC. 21:00 UTC is 23:00 in Madrid, before its next morning.
            var thursday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 22, 21, 0, 0, TimeSpan.Zero), default);
            // 21:00 UTC is 06:00 in Tokyo: Thursday's overview waits for 07:00 there, then goes out once.
            Assert.DoesNotContain(sender.Sent, m => m.To == tokyo.Email && m.Subject.EndsWith("22 Oct 2026"));
            var tokyoMorning = new Captured();
            await Service(scope, tokyoMorning).SendOverviewsAsync(new DateTimeOffset(2026, 10, 22, 22, 30, 0, TimeSpan.Zero), default);
            Assert.Single(tokyoMorning.Sent, m => m.To == tokyo.Email);
            var morning = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 5, 30, 0, TimeSpan.Zero), default);
            // Friday evening: Friday's session (Tokyo's waits for its Saturday morning). Monday morning adds nothing: still Friday's.
            var friday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero), default);
            var saturday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero), default);
            var monday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero), default);

            Assert.True(thursday >= 1);
            Assert.Contains(sender.Sent, m => m.To == wanted.Email && m.Subject == "Your daily overview, 22 Oct 2026");
            Assert.Contains(sender.Sent, m => m.To == wanted.Email && m.Subject == "Your daily overview, 23 Oct 2026");
            Assert.DoesNotContain(sender.Sent, m => m.To == off.Email || m.To == unverified.Email);
            Assert.Equal(0, morning);
            Assert.True(friday >= 1);
            Assert.Equal((1, 0), (saturday, monday)); // Saturday: only Tokyo's Friday overview, which waited for its morning.
            Assert.Contains(sender.Sent, m => m.To == tokyo.Email && m.Subject.EndsWith("23 Oct 2026"));
            var mail = sender.Sent.First(m => m.To == wanted.Email);
            Assert.Contains("Waiting for you", mail.Body); // The seeded live signal is still waiting.
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.UserId == wanted.UserId && n.Kind == NotificationKind.Overview));
        }
        finally
        {
            foreach (var seeded in new[] { wanted, off, unverified, tokyo }) await CleanAsync(db, seeded.UserId, seeded.AssetId);
        }
    }

    [NeonFact]
    public async Task A_snoozed_signal_is_reminded_once_from_seven_on_its_snooze_day_while_it_waits()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var snoozed = await SeedAsync(db);
        var answered = await SeedAsync(db);
        try
        {
            var monday = new DateOnly(2026, 10, 26);
            await db.Signals.Where(s => s.Id == snoozed.SignalId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SnoozedUntil, monday));
            await db.Signals.Where(s => s.Id == answered.SignalId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Skipped)
                .SetProperty(x => x.AnsweredAt, DateTimeOffset.UtcNow));
            var sender = new Captured();

            // Madrid is UTC+1 on 2026-10-26: 05:30 UTC is 06:30 there, 06:30 UTC is 07:30.
            await Service(scope, sender).SendSnoozeRemindersAsync(new DateTimeOffset(2026, 10, 26, 5, 30, 0, TimeSpan.Zero), default);
            Assert.DoesNotContain(sender.Sent, m => m.To == snoozed.Email);
            // Two days late: a reminder for a day gone by isn't sent.
            await Service(scope, sender).SendSnoozeRemindersAsync(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero), default);
            Assert.DoesNotContain(sender.Sent, m => m.To == snoozed.Email);
            await Service(scope, sender).SendSnoozeRemindersAsync(new DateTimeOffset(2026, 10, 26, 6, 30, 0, TimeSpan.Zero), default);
            await Service(scope, sender).SendSnoozeRemindersAsync(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero), default);

            var mail = Assert.Single(sender.Sent, m => m.To == snoozed.Email);
            Assert.StartsWith("Reminder: Test asset", mail.Subject);
            Assert.Contains($"/strategies/{snoozed.StrategyId}/signals/{snoozed.SignalId}", mail.Body);
            Assert.DoesNotContain(sender.Sent, m => m.To == answered.Email);
            var row = await db.Notifications.SingleAsync(n => n.UserId == snoozed.UserId);
            Assert.Equal((NotificationKind.Snoozed, monday), (row.Kind, row.ForDate));
        }
        finally
        {
            foreach (var seeded in new[] { snoozed, answered }) await CleanAsync(db, seeded.UserId, seeded.AssetId);
        }
    }

    [NeonFact]
    public async Task An_alert_that_could_not_be_sent_because_email_is_not_set_up_is_tried_again_later()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, _, assetId, _, _) = await SeedAsync(db);
        try
        {
            var sender = new NotSetUpThenWorks();
            var service = ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (IEmailSender)sender);

            var before = await service.SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            Assert.Equal(0, await db.Notifications.CountAsync(n => n.UserId == userId)); // The claim was given back.
            sender.SetUp = true;
            var after = await service.SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            var again = await service.SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);

            Assert.Equal((0, 1, 0), (before, after, again));
            Assert.Equal(1, sender.Sent);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task No_overview_before_the_daily_check_has_evaluated_the_session_and_one_once_it_has()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId, _, _) = await SeedAsync(db, evaluated: false);
        try
        {
            var sender = new Captured();
            var morning = new DateTimeOffset(2026, 10, 23, 5, 30, 0, TimeSpan.Zero);

            var before = await Service(scope, sender).SendOverviewsAsync(morning, default);
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategyId, AssetId = assetId, Date = new DateOnly(2026, 10, 22), Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("{}") });
            await db.SaveChangesAsync();
            var after = await Service(scope, sender).SendOverviewsAsync(morning, default);

            Assert.Equal(0, before);
            Assert.True(after >= 1);
            Assert.Contains("(Live)", sender.Sent.Single(m => m.Subject.StartsWith("Your daily overview")).Body.Replace("Waiting for you", ""));
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [Fact]
    public void The_hourly_check_waits_until_the_next_top_of_the_hour()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), NotificationWorker.UntilNextHour(new DateTimeOffset(2026, 10, 23, 5, 30, 0, TimeSpan.Zero)));
        Assert.Equal(TimeSpan.FromHours(1), NotificationWorker.UntilNextHour(new DateTimeOffset(2026, 10, 23, 5, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void An_amount_in_another_currency_carries_its_code_and_a_distance_is_never_zero_in_the_list()
    {
        var other = Dip() with { StrategyCurrency = "CHF", StepAmount = 100 };

        Assert.Contains("(CHF 100)", EmailTexts.SignalText(other));
    }
}
