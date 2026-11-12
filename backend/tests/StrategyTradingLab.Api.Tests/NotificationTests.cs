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
        Assert.Contains("https://app.example/en/strategies/11111111-1111-1111-1111-111111111111/signals/22222222-2222-2222-2222-222222222222", body);
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
    public void The_summary_has_its_sections_describes_and_never_instructs()
    {
        var waiting = new[] { Dip() };
        var changed = new[] { Dip() with { Mode = StrategyMode.Simulation, StrategyName = "The comeback" } };
        var score = new[] { new ScoreLine("Zscaler & friends", StrategyMode.Live, new DateOnly(2026, 10, 1), 1), new ScoreLine("The comeback", StrategyMode.Simulation, new DateOnly(2026, 10, 5), 0) };

        var (subject, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), waiting, changed, score, "https://app.example");

        Assert.Equal("Your summary, 23 Oct 2026", subject);
        foreach (var section in new[] { "Waiting for you", "What changed today", "Your strategies" }) Assert.Contains(section, body);
        Assert.DoesNotContain("Close to the line", body);
        Assert.Contains("(Simulation) Your rules for The comeback", body);
        Assert.Contains("The comeback (Simulation): since 5 Oct 2026, 0 signals", body);
        Assert.EndsWith("Not financial advice. Signals follow your rules.", body);
        Assert.DoesNotContain("buy now", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_summary_without_changes_leaves_what_changed_out()
    {
        var (_, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [Dip()], [], [], "https://app.example");

        Assert.DoesNotContain("What changed today", body);
    }

    [Fact]
    public void The_catalan_summary_uses_straight_apostrophe_and_catalan_sections()
    {
        var waiting = new[] { Dip() };
        var changed = new[] { Dip() with { Mode = StrategyMode.Simulation, StrategyName = "The comeback" } };
        var score = new[]
        {
            new ScoreLine("Zscaler & friends", StrategyMode.Live, new DateOnly(2026, 10, 1), 1),
            new ScoreLine("The comeback", StrategyMode.Simulation, new DateOnly(2026, 10, 5), 0),
        };

        var (subject, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), waiting, changed, score, "https://app.example", "ca");

        Assert.Equal("El teu resum, 23 oct. 2026", subject);
        Assert.DoesNotContain("T’espera a tu", body);
        foreach (var section in new[] { "T'espera a tu", "Què ha canviat avui", "Les teves estratègies" }) Assert.Contains(section, body);
        Assert.Contains("Zscaler & friends (Manual): des del 1 oct. 2026, 1 senyal", body);
        Assert.Contains("The comeback (Simulació): des del 5 oct. 2026, 0 senyals", body);
        Assert.EndsWith(EmailTexts.DisclaimerCa, body);
    }

    [Fact]
    public void A_stock_change_push_names_the_stock_opens_its_page_and_has_one_tag_per_stock()
    {
        var tesla = new StockChangeLine(Guid.NewGuid(), Guid.NewGuid(), "Strat", StrategyMode.Live, "dip", "Tesla", StockChangeKind.CameClose, 2.5m);
        var close = PushTexts.StockChange(tesla, "en");
        Assert.Equal(("Getting close: Tesla", "Tesla, Strat: now 2.5% above the discount zone. (Manual)"), (close.Title, close.Body));
        Assert.Equal($"/strategies/{tesla.StrategyId}/stocks/{tesla.AssetId}", close.Url);

        // Becoming a candidate replaces getting close on the device, and the other way round: the same stock, the same tag.
        var candidate = PushTexts.StockChange(tesla with { Kind = StockChangeKind.BecameCandidate, DistancePct = null, Mode = StrategyMode.Simulation }, "ca");
        Assert.Equal(("Nova candidata: Tesla", "Tesla, Strat: ara és candidata, les regles la vigilen. (Simulació)"), (candidate.Title, candidate.Body));
        Assert.Equal(close.Tag, candidate.Tag);
        Assert.Equal("A prop de la línia: Tesla", PushTexts.StockChange(tesla, "ca").Title);
    }

    [Fact]
    public void Stock_change_text_formats_candidate_and_distance_changes()
    {
        var candidate = new StockChangeLine(Guid.NewGuid(), Guid.NewGuid(), "Strat", StrategyMode.Live, "dip", "Apple", StockChangeKind.BecameCandidate);
        Assert.Equal("Apple, Strat: now a candidate, the rules watch it.", EmailTexts.StockChangeText(candidate, "en"));
        Assert.Equal("Apple, Strat: ara és candidata, les regles la vigilen.", EmailTexts.StockChangeText(candidate, "ca"));

        var close = new StockChangeLine(Guid.NewGuid(), Guid.NewGuid(), "Strat", StrategyMode.Live, "dip", "Tesla", StockChangeKind.CameClose, 2.5m);
        Assert.Equal("Tesla, Strat: now 2.5% above the discount zone.", EmailTexts.StockChangeText(close, "en"));
        Assert.Equal("Tesla, Strat: ara un 2.5% per sobre de la zona de descompte.", EmailTexts.StockChangeText(close, "ca"));
    }

    [Fact]
    public void An_email_with_a_token_has_an_unsubscribe_link_and_the_one_click_address()
    {
        var alert = EmailTexts.SignalAlert(Dip(), "https://app.example", "en", "tok3n");
        var overview = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], "https://app.example", "ca", unsubscribeToken: "tok3n");
        var none = EmailTexts.SignalAlert(Dip(), "https://app.example");

        Assert.Contains("Turn off signal and reminder emails: https://app.example/en/unsubscribe/tok3n/alerts", alert.Text);
        Assert.Contains("href=\"https://app.example/en/unsubscribe/tok3n/alerts\"", alert.Html);
        Assert.Equal("https://app.example/api/email/unsubscribe/tok3n/alerts", alert.OneClickUnsubscribe);
        Assert.Contains("Desactiva els mails de resum: https://app.example/ca/unsubscribe/tok3n/overview", overview.Text);
        Assert.EndsWith(EmailTexts.DisclaimerCa, overview.Text);
        Assert.Null(none.OneClickUnsubscribe);
        Assert.DoesNotContain("unsubscribe", none.Text);
        Assert.Equal(alert.OneClickUnsubscribe, new OutgoingEmail("a@b.c", alert).UnsubscribeUrl);
    }

    [Fact]
    public void An_empty_waiting_list_leaves_that_section_out()
    {
        var (_, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], "https://app.example");

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
        Assert.Contains("My AI picks: https://app.example/en/strategies/11111111-1111-1111-1111-111111111111/ai-round", body);
        Assert.EndsWith("Not financial advice. Signals follow your rules.", body);
    }

    [Fact]
    public void The_ai_round_email_and_signal_are_in_catalan_for_a_catalan_user()
    {
        var (subject, body, _) = EmailTexts.AiRoundDue([(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Les meves IA")], "https://app.example", "ca");
        var signal = Dip("""{"score":91,"rank":1}""") with { TemplateKey = "ai" };

        Assert.Equal("És l'hora de la ronda d'IA de Les meves IA", subject);
        Assert.EndsWith(EmailTexts.DisclaimerCa, body);
        Assert.Contains("https://app.example/ca/strategies/11111111-1111-1111-1111-111111111111/ai-round", body); // Opens in Catalan.
        Assert.Equal("la teva IA l'ha puntuat amb 91 sobre 100, la puntuació més alta de la ronda", EmailTexts.FactSentence(signal, "ca"));
        Assert.Equal("your AI scored it 91 out of 100, the top score of the round", EmailTexts.FactSentence(signal));
    }

    [Fact]
    public void The_overview_lists_ai_rounds_due_with_the_signals_waiting()
    {
        var (_, body, _) = EmailTexts.Overview(new DateOnly(2026, 10, 23), [], [], [], "https://app.example", "en",
            [(Guid.Parse("11111111-1111-1111-1111-111111111111"), "My AI picks")]);

        Assert.Contains("Waiting for you\n- Time for the AI round of My AI picks. https://app.example/en/strategies/11111111-1111-1111-1111-111111111111/ai-round", body);
    }

    sealed class Captured : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
        {
            Sent.Add((email.To, email.Subject, email.Body));
            return Task.CompletedTask;
        }
    }

    sealed class Suppressed : IEmailSender
    {
        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken) => throw new EmailSuppressedException();
    }

    sealed class NotSetUpThenWorks : IEmailSender
    {
        public bool SetUp { get; set; }
        public int Sent { get; private set; }
        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
        {
            if (!SetUp) throw new EmailNotSetUpException();
            Sent++;
            return Task.CompletedTask;
        }
    }

    static async Task<(Guid UserId, Guid StrategyId, Guid AssetId, Guid SignalId, string Email)> SeedAsync(AppDbContext db, bool verified = true, bool overview = true,
        StrategyMode mode = StrategyMode.Live, string timeZone = "Europe/Madrid", bool evaluated = true, bool alerts = true, string templateKey = "dip",
        int? notifyHour = null)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"N{tag}", Exchange = "TEST", Name = "Test asset", Currency = "USD" };
        var user = new User { Email = $"notify-{tag}@example.com", TimeZone = timeZone, Locale = "en", OverviewEmail = overview, AlertEmails = alerts, EmailVerifiedAt = verified ? DateTimeOffset.UtcNow : null, NotifyHour = notifyHour };
        db.Assets.Add(asset);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = templateKey, Name = $"Notify {tag}", Mode = mode, Status = StrategyStatus.Running, StartedOn = new DateOnly(2026, 10, 1),
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

    static NotificationService Service(IServiceScope scope, IEmailSender sender) =>
        ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (IEmailSender)sender);

    static NotificationService Service(IServiceScope scope, IEmailSender sender, DateTimeOffset now) =>
        ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (IEmailSender)sender, (TimeProvider)new FixedTime(now));

    sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static NotificationService Service(IServiceScope scope, IPushSender push) =>
        ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (IEmailSender)new Captured(), push);

    /// <summary>Delivers to every device but those whose endpoint says "gone".</summary>
    sealed class FakePush : IPushSender
    {
        public string? PublicKey => "key";
        public List<(string Endpoint, string Payload)> Sent { get; } = [];
        public Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken)
        {
            if (subscription.Endpoint.Contains("gone")) return Task.FromResult(PushOutcome.Gone);
            Sent.Add((subscription.Endpoint, payload));
            return Task.FromResult(PushOutcome.Sent);
        }
    }

    /// <summary>Fails while <see cref="Down"/> is set, as a push service that is down does.</summary>
    sealed class FlakyPush : IPushSender
    {
        public bool Down { get; set; } = true;
        public bool Refuse { get; set; }
        public string? PublicKey => "key";
        public Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken) =>
            Task.FromResult(Refuse ? PushOutcome.Refused : Down ? PushOutcome.Failed : PushOutcome.Sent);
    }

    [NeonFact]
    public async Task A_push_that_failed_is_tried_again_and_a_device_failing_for_a_week_is_dropped()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId, _, _) = await SeedAsync(db);
        try
        {
            var device = new PushSubscription { UserId = userId, Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}", P256dh = "p", Auth = "a" };
            db.PushSubscriptions.Add(device);
            await db.SaveChangesAsync();
            var push = new FlakyPush();

            // Down: nothing sent, the claim given back and the device marked as failing since now.
            Assert.Equal(0, await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            Assert.False(await db.Notifications.AnyAsync(n => n.UserId == userId));
            Assert.NotNull(await db.PushSubscriptions.Where(p => p.Id == device.Id).Select(p => p.LastFailedAt).SingleAsync());

            // Back up: the next run sends it and clears the mark.
            push.Down = false;
            Assert.Equal(1, await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            Assert.Null(await db.PushSubscriptions.Where(p => p.Id == device.Id).Select(p => p.LastFailedAt).SingleAsync());

            // A refusal (a key the device no longer matches) isn't tried again every hour: the claim stays, as failed.
            await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync();
            push.Refuse = true;
            Assert.Equal(0, await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            Assert.NotNull(await db.Notifications.Where(n => n.UserId == userId).Select(n => n.FailedAt).SingleAsync());
            Assert.Equal(0, await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            push.Refuse = false;

            // A device failing for over a week is forgotten on its next failure.
            await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync();
            await db.PushSubscriptions.Where(p => p.Id == device.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.LastFailedAt, DateTimeOffset.UtcNow.AddDays(-8)));
            push.Down = true;
            Assert.Equal(0, await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            Assert.False(await db.PushSubscriptions.AnyAsync(p => p.Id == device.Id));
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task A_live_signal_is_pushed_once_to_each_device_when_the_user_has_push_on()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId, signalId, _) = await SeedAsync(db);
        try
        {
            var tag = Guid.NewGuid().ToString("N");
            db.PushSubscriptions.AddRange(
                new PushSubscription { UserId = userId, Endpoint = $"https://fcm.googleapis.com/fcm/send/{tag}", P256dh = "p", Auth = "a" },
                new PushSubscription { UserId = userId, Endpoint = $"https://fcm.googleapis.com/fcm/send/gone-{tag}", P256dh = "p", Auth = "a" });
            await db.SaveChangesAsync();
            var push = new FakePush();

            // With the user's App box off, nothing goes out and nothing is claimed.
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.AlertPush, false));
            Assert.Equal(0, await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.AlertPush, true));

            var first = await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            var second = await Service(scope, push).PushSignalAlertsAsync(new DateOnly(2026, 10, 23), default);

            Assert.Equal((1, 0), (first, second));
            var (endpoint, payload) = Assert.Single(push.Sent);
            Assert.EndsWith(tag, endpoint);
            using var message = JsonDocument.Parse(payload);
            Assert.Equal($"/strategies/{strategyId}/signals/{signalId}", message.RootElement.GetProperty("url").GetString());
            Assert.StartsWith("Your rules for Notify", message.RootElement.GetProperty("body").GetString());
            // The device the push service called gone is forgotten.
            Assert.Equal(1, await db.PushSubscriptions.CountAsync(p => p.UserId == userId));
            var row = await db.Notifications.SingleAsync(n => n.UserId == userId);
            Assert.Equal((NotificationKind.Signal, NotificationChannel.Push), (row.Kind, row.Channel));
            Assert.NotNull(row.SentAt);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task A_stock_is_pushed_once_when_it_changes_state_and_not_again_while_it_stays_there()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // A simulation: stock changes are pushed for both modes.
        var (userId, strategyId, assetId, _, _) = await SeedAsync(db, mode: StrategyMode.Simulation);
        try
        {
            db.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}", P256dh = "p", Auth = "a" });
            // Thursday not a candidate, Friday a candidate (3.2% away, not close yet).
            await db.DailyEvaluations.Where(e => e.StrategyId == strategyId && e.Date == new DateOnly(2026, 10, 22))
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.Status, EvaluationStatus.NotCandidate));
            await db.SaveChangesAsync();
            var push = new FakePush();
            async Task<int> Run(DateTimeOffset now) => await Service(scope, push).PushStockChangesAsync(now, default);

            // With push off, nothing goes out and nothing is claimed.
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.AlertPush, false));
            var saturday = new DateTimeOffset(2026, 10, 24, 5, 0, 0, TimeSpan.Zero);
            Assert.Equal(0, await Run(saturday));
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.AlertPush, true));

            // Not a candidate -> a candidate: once, whatever the hour, and not again on a second run for the session.
            Assert.Equal((1, 0), (await Run(saturday), await Run(saturday.AddHours(1))));
            using (var message = JsonDocument.Parse(Assert.Single(push.Sent).Payload))
            {
                Assert.StartsWith("New candidate: Test asset", message.RootElement.GetProperty("title").GetString());
                Assert.Equal($"/strategies/{strategyId}/stocks/{assetId}", message.RootElement.GetProperty("url").GetString());
            }
            var row = await db.Notifications.SingleAsync(n => n.UserId == userId);
            Assert.Equal((NotificationKind.StockCandidate, NotificationChannel.Push, strategyId, assetId), (row.Kind, row.Channel, row.StrategyId, row.AssetId));

            // Monday still a candidate, still 3.2% away: nothing changed, nothing pushed.
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategyId, AssetId = assetId, Date = new DateOnly(2026, 10, 26), Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("{\"distancePct\":3.2}") });
            await db.SaveChangesAsync();
            Assert.Equal(0, await Run(new DateTimeOffset(2026, 10, 27, 5, 0, 0, TimeSpan.Zero)));

            // Tuesday within 3%: getting close, once.
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategyId, AssetId = assetId, Date = new DateOnly(2026, 10, 27), Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("{\"distancePct\":1.5}") });
            await db.SaveChangesAsync();
            var wednesday = new DateTimeOffset(2026, 10, 28, 5, 0, 0, TimeSpan.Zero);
            Assert.Equal((1, 0), (await Run(wednesday), await Run(wednesday)));
            Assert.StartsWith("{\"title\":\"Getting close: Test asset\"", push.Sent[^1].Payload);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task A_live_signal_is_alerted_once_at_the_users_hour_and_recorded()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, _, assetId, signalId, email) = await SeedAsync(db, notifyHour: 7);
        try
        {
            var sender = new Captured();
            // Thursday 22 October's signal waits for 07:00 on Friday in Madrid (05:00 UTC): at 23:00 there, nothing goes out.
            Assert.Equal(0, await Service(scope, sender, new DateTimeOffset(2026, 10, 22, 21, 0, 0, TimeSpan.Zero)).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default));
            Assert.Empty(sender.Sent);
            var first = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            var second = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);

            Assert.Equal((1, 0), (first, second));
            Assert.Equal(email, Assert.Single(sender.Sent).To);
            var token = await db.Users.Where(u => u.Id == userId).Select(u => u.UnsubscribeToken).SingleAsync();
            Assert.Contains($"/unsubscribe/{token}/alerts", sender.Sent[0].Body);
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
    public async Task A_second_signal_for_the_same_stock_the_same_day_is_not_alerted_and_an_alerted_buy_that_ends_is_told_once()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId, signalId, _) = await SeedAsync(db);
        try
        {
            var today = new DateOnly(2026, 10, 23);
            var sender = new Captured();
            Assert.Equal(1, await Service(scope, sender).SendSignalAlertsAsync(today, default));
            // An extra check the same day (a price that came in late) signals the same stock again, for the next session.
            db.Signals.Add(new Signal
            {
                StrategyId = strategyId, AssetId = assetId, Side = TradeSide.Buy, StepNo = 2, MaxSteps = 5, StepAmount = 100, SignalDate = today,
                ClosePrice = 187.5m, Facts = JsonDocument.Parse("{}"), Status = SignalStatus.Waiting,
            });
            await db.SaveChangesAsync();
            Assert.Equal(0, await Service(scope, sender).SendSignalAlertsAsync(today, default));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == userId && n.SentAt == null && n.Error != null));

            await db.Signals.Where(s => s.Id == signalId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SignalStatus.Missed).SetProperty(x => x.EndedOn, today));
            var first = await Service(scope, sender).SendSignalEndedAsync(today, default);
            var second = await Service(scope, sender).SendSignalEndedAsync(today, default);

            Assert.Equal((1, 0), (first, second));
            Assert.Equal(2, sender.Sent.Count);
            Assert.Contains("the buy signal for", sender.Sent[1].Subject);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task An_alert_to_a_suppressed_address_is_recorded_as_skipped_not_sent_or_failed()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, _, assetId, _, _) = await SeedAsync(db);
        try
        {
            var sent = await Service(scope, new Suppressed()).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);

            Assert.Equal(0, sent);
            var row = await db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == userId);
            Assert.Null(row.SentAt);
            Assert.Null(row.FailedAt);
            Assert.Contains("suppression list", row.Error);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task No_alert_or_reminder_for_someone_who_turned_alerts_off()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, _, assetId, signalId, _) = await SeedAsync(db, alerts: false);
        try
        {
            await db.Signals.Where(s => s.Id == signalId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SnoozedUntil, new DateOnly(2026, 10, 23)));
            var sender = new Captured();

            var alerts = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            var reminders = await Service(scope, sender).SendSnoozeRemindersAsync(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero), default);

            Assert.Equal((0, 0), (alerts, reminders));
            Assert.Empty(sender.Sent);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task No_alert_for_an_unverified_address_a_simulation_or_a_user_with_alert_emails_off()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unverified = await SeedAsync(db, verified: false);
        var simulation = await SeedAsync(db, mode: StrategyMode.Simulation);
        var off = await SeedAsync(db, alerts: false);
        try
        {
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
    public async Task No_alert_or_snooze_reminder_for_an_ai_strategy_signal()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ai = await SeedAsync(db, templateKey: "ai");
        try
        {
            await db.Signals.Where(s => s.Id == ai.SignalId).ExecuteUpdateAsync(s => s.SetProperty(x => x.SnoozedUntil, new DateOnly(2026, 10, 23)));
            var sender = new Captured();

            var alerts = await Service(scope, sender).SendSignalAlertsAsync(new DateOnly(2026, 10, 23), default);
            var snoozes = await Service(scope, sender).SendSnoozeRemindersAsync(new DateTimeOffset(2026, 10, 23, 7, 30, 0, TimeSpan.Zero), default);

            Assert.Equal((0, 0), (alerts, snoozes));
            Assert.Empty(sender.Sent);
        }
        finally
        {
            await CleanAsync(db, ai.UserId, ai.AssetId);
        }
    }

    [NeonFact]
    public async Task The_summary_goes_out_once_a_session_at_the_users_hour_or_right_after_the_check_and_not_on_a_quiet_day()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wanted = await SeedAsync(db, notifyHour: 7);
        var asap = await SeedAsync(db);
        var off = await SeedAsync(db, overview: false);
        var unverified = await SeedAsync(db, verified: false);
        var tokyo = await SeedAsync(db, timeZone: "Asia/Tokyo", notifyHour: 7);
        // Kathmandu is UTC+5:45: Thursday's summary at 02:00 there is due at 20:15 UTC on Friday, after no hourly run and just
        // before Friday's close (20:30 UTC); it still goes out once Friday's session is the last one.
        var kathmandu = await SeedAsync(db, timeZone: "Asia/Kathmandu", notifyHour: 2);
        try
        {
            var sender = new Captured();
            // New York is UTC-4 on 2026-10-22: the bar is final at 20:30 UTC. At 21:00 UTC only "as soon as ready" gets Thursday's
            // summary: it is 23:00 in Madrid and 06:00 in Tokyo, before their 07:00.
            var thursday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 22, 21, 0, 0, TimeSpan.Zero), default);
            Assert.Contains(sender.Sent, m => m.To == asap.Email && m.Subject == "Your summary, 22 Oct 2026");
            Assert.DoesNotContain(sender.Sent, m => m.To == wanted.Email || m.To == tokyo.Email);
            var tokyoMorning = new Captured();
            await Service(scope, tokyoMorning).SendOverviewsAsync(new DateTimeOffset(2026, 10, 22, 22, 30, 0, TimeSpan.Zero), default);
            Assert.Single(tokyoMorning.Sent, m => m.To == tokyo.Email);
            // 05:30 UTC on Friday is 07:30 in Madrid.
            await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 5, 30, 0, TimeSpan.Zero), default);
            Assert.Contains(sender.Sent, m => m.To == wanted.Email && m.Subject == "Your summary, 22 Oct 2026");
            // Friday's session had no signal and no stock change, so nobody gets a summary; its claim says why.
            var friday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero), default);
            var saturday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 24, 6, 0, 0, TimeSpan.Zero), default);
            var monday = await Service(scope, sender).SendOverviewsAsync(new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero), default);

            Assert.True(thursday >= 1);
            Assert.DoesNotContain(sender.Sent, m => m.Subject.EndsWith("23 Oct 2026"));
            Assert.DoesNotContain(sender.Sent, m => m.To == off.Email || m.To == unverified.Email);
            Assert.Equal((1, 0, 0), (friday, saturday, monday));
            Assert.Single(sender.Sent, m => m.To == kathmandu.Email && m.Subject == "Your summary, 22 Oct 2026");
            var mail = sender.Sent.First(m => m.To == wanted.Email);
            Assert.Contains("Waiting for you", mail.Body); // The seeded live signal is still waiting.
            foreach (var user in new[] { wanted, asap })
            {
                Assert.Equal(2, await db.Notifications.CountAsync(n => n.UserId == user.UserId && n.Kind == NotificationKind.Overview));
                var quiet = await db.Notifications.SingleAsync(n => n.UserId == user.UserId && n.Kind == NotificationKind.Overview && n.ForDate == new DateOnly(2026, 10, 23));
                Assert.Equal((null, "Not sent: nothing changed."), (quiet.SentAt, quiet.Error));
            }
        }
        finally
        {
            foreach (var seeded in new[] { wanted, asap, off, unverified, tokyo, kathmandu }) await CleanAsync(db, seeded.UserId, seeded.AssetId);
        }
    }

    [NeonFact]
    public async Task A_quiet_day_is_only_final_once_every_strategy_was_checked_for_the_session()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeded = await SeedAsync(db);
        try
        {
            // A second strategy the daily check hasn't reached yet for Friday 23 October. (One it never checks, with nothing to
            // check, doesn't count: it would hold the verdict forever.)
            var late = new Strategy
            {
                UserId = seeded.UserId, TemplateKey = "dip", Name = $"Late {Guid.NewGuid():N}"[..20], Mode = StrategyMode.Live, Status = StrategyStatus.Running,
                StartedOn = new DateOnly(2026, 10, 1), Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
                Assets = [new StrategyAsset { AssetId = seeded.AssetId, Active = true, AddedAt = DateTimeOffset.UtcNow }],
            };
            db.Strategies.Add(late);
            // Checked on Thursday, so the check is still due to reach it for Friday.
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = late.Id, AssetId = seeded.AssetId, Date = new DateOnly(2026, 10, 22), Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("{}") });
            await db.SaveChangesAsync();
            var friday = new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero);
            bool Claimed() => db.Notifications.Any(n => n.UserId == seeded.UserId && n.Kind == NotificationKind.Overview && n.ForDate == new DateOnly(2026, 10, 23));

            // Quiet so far, but not every strategy was checked: the claim is given back, nothing is sent.
            Assert.Equal(0, await Service(scope, new Captured()).SendOverviewsAsync(friday, default));
            Assert.False(Claimed());

            // Once checked (still quiet), the verdict is final and kept.
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = late.Id, AssetId = seeded.AssetId, Date = new DateOnly(2026, 10, 23), Status = EvaluationStatus.Watching, Facts = JsonDocument.Parse("{}") });
            await db.SaveChangesAsync();
            Assert.Equal(0, await Service(scope, new Captured()).SendOverviewsAsync(friday, default));
            Assert.True(Claimed());
        }
        finally
        {
            await CleanAsync(db, seeded.UserId, seeded.AssetId);
        }
    }

    [NeonFact]
    public async Task A_strategy_the_check_never_reaches_does_not_hold_a_quiet_day()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeded = await SeedAsync(db);
        try
        {
            // Running for weeks with nothing checked (every stock unticked): no evaluation in the recent days.
            db.Strategies.Add(new Strategy
            {
                UserId = seeded.UserId, TemplateKey = "dip", Name = $"Empty {Guid.NewGuid():N}"[..20], Mode = StrategyMode.Simulation, Status = StrategyStatus.Running,
                StartedOn = new DateOnly(2026, 10, 1), Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            });
            await db.SaveChangesAsync();

            await Service(scope, new Captured()).SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero), default);
            var quiet = await db.Notifications.SingleAsync(n => n.UserId == seeded.UserId && n.Kind == NotificationKind.Overview && n.ForDate == new DateOnly(2026, 10, 23));
            Assert.Equal("Not sent: nothing changed.", quiet.Error);
        }
        finally
        {
            await CleanAsync(db, seeded.UserId, seeded.AssetId);
        }
    }

    [NeonFact]
    public async Task A_quiet_day_waits_for_a_strategy_started_that_day_to_be_checked()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeded = await SeedAsync(db);
        try
        {
            var friday = new DateOnly(2026, 10, 23);
            db.Strategies.Add(new Strategy
            {
                UserId = seeded.UserId, TemplateKey = "dip", Name = $"New {Guid.NewGuid():N}"[..20], Mode = StrategyMode.Simulation, Status = StrategyStatus.Running,
                StartedOn = friday, Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
                Assets = [new StrategyAsset { AssetId = seeded.AssetId, Active = true, AddedAt = DateTimeOffset.UtcNow }],
            });
            await db.SaveChangesAsync();

            Assert.Equal(0, await Service(scope, new Captured()).SendOverviewsAsync(new DateTimeOffset(2026, 10, 23, 21, 0, 0, TimeSpan.Zero), default));
            Assert.False(db.Notifications.Any(n => n.UserId == seeded.UserId && n.Kind == NotificationKind.Overview && n.ForDate == friday));
        }
        finally
        {
            await CleanAsync(db, seeded.UserId, seeded.AssetId);
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
            Assert.Contains("(Manual)", sender.Sent.Single(m => m.Subject.StartsWith("Your summary")).Body.Replace("Waiting for you", ""));
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [Fact]
    public void What_a_check_finds_waits_for_the_first_chosen_hour_after_the_close()
    {
        // Thursday 22 October 2026: the bar is final at 20:30 UTC (16:30 in New York).
        var thursday = new DateOnly(2026, 10, 22);
        var final = new DateTimeOffset(2026, 10, 22, 20, 30, 0, TimeSpan.Zero);
        Assert.Equal(final, NotifyTime.DueAt(thursday, null, "Europe/Madrid"));
        // 07:00 on Friday in Madrid (UTC+2) and in New York (UTC-4); 07:00 on Friday in Tokyo comes 90 minutes after the close.
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 5, 0, 0, TimeSpan.Zero), NotifyTime.DueAt(thursday, 7, "Europe/Madrid"));
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 11, 0, 0, TimeSpan.Zero), NotifyTime.DueAt(thursday, 7, "America/New_York"));
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 22, 0, 0, TimeSpan.Zero), NotifyTime.DueAt(thursday, 7, "Asia/Tokyo"));
        // 23:00 in Madrid is after that night's close (22:30 there): the same evening.
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 21, 0, 0, TimeSpan.Zero), NotifyTime.DueAt(thursday, 23, "Europe/Madrid"));
        // Friday 23 October's summary in Madrid comes on Saturday, and reminders keep 07:00 for "as soon as ready".
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 5, 0, 0, TimeSpan.Zero), NotifyTime.DueAt(new DateOnly(2026, 10, 23), 7, "Europe/Madrid"));
        Assert.Equal((7, 9), (NotifyTime.ReminderHour(null), NotifyTime.ReminderHour(9)));
        // An alert never waits past 2 hours before the next close (Friday 20:30 UTC), when the next check could end its signal:
        // Kathmandu's 02:00 (20:15 UTC on Friday) becomes 18:30 UTC; Madrid's 07:00 is long before.
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 18, 30, 0, TimeSpan.Zero), NotifyTime.AlertDueAt(thursday, 2, "Asia/Kathmandu"));
        Assert.Equal(NotifyTime.DueAt(thursday, 7, "Europe/Madrid"), NotifyTime.AlertDueAt(thursday, 7, "Europe/Madrid"));
        // Friday's alerts may wait over the weekend until 2 hours before Monday's close.
        Assert.Equal(NotifyTime.DueAt(new DateOnly(2026, 10, 23), 7, "Europe/Madrid"), NotifyTime.AlertDueAt(new DateOnly(2026, 10, 23), 7, "Europe/Madrid"));
        Assert.False(NotifyTime.IsHour(24));
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
