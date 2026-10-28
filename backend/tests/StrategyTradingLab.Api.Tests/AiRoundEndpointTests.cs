using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Ai;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Strategies;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Your AI's top picks, end to end: start a strategy, run its AI round through the API, then the daily check.</summary>
public class AiRoundEndpointTests(AccountsApi factory) : IClassFixture<AccountsApi>
{
    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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

    record Seeded(HttpClient Client, string Email, Guid StrategyId, Guid Alpha, Guid Beta, string Tag);

    /// <summary>Two euro test stocks with a month of prices up to the last finished session, and a new AI strategy watching them.</summary>
    async Task<Seeded> StartAsync(string mode, object? ai = null, object? settings = null)
    {
        var (client, email) = await factory.SignedUpAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid alpha, beta;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = new Asset { Symbol = $"AL{tag}", Exchange = "TEST", Name = $"Alpha {tag}", Currency = "EUR" };
            var b = new Asset { Symbol = $"BE{tag}", Exchange = "TEST", Name = $"Beta {tag}", Currency = "EUR" };
            db.Assets.AddRange(a, b);
            await db.SaveChangesAsync();
            var session = UsSession.LastComplete(DateTimeOffset.UtcNow);
            for (var day = session.AddDays(-30); day <= session; day = day.AddDays(1))
            {
                db.Prices.Add(new PriceDaily { AssetId = a.Id, Date = day, Open = 100, High = 100, Low = 100, Close = 100, Volume = 1 });
                db.Prices.Add(new PriceDaily { AssetId = b.Id, Date = day, Open = 50, High = 50, Low = 50, Close = 50, Volume = 1 });
            }
            await db.SaveChangesAsync();
            (alpha, beta) = (a.Id, b.Id);
        }
        var response = await client.PostAsJsonAsync("/strategies", new
        {
            templateKey = "ai", name = $"AI {tag}", mode, budget = new { amount = 3000, step = 100, maxSteps = 5 }, assetIds = new[] { alpha, beta },
            currency = "EUR", settings = settings ?? new { }, ai = ai ?? new { news = true, mood = false, figures = true, instructions = (string?)null },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return new Seeded(client, email, id, alpha, beta, tag);
    }

    async Task CleanAsync(Seeded seeded)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Strategies.Where(s => s.Id == seeded.StrategyId).ExecuteDeleteAsync();
        await db.Assets.Where(a => a.Id == seeded.Alpha || a.Id == seeded.Beta).ExecuteDeleteAsync();
        // A shared round has nothing that points back to its user: found by the test stocks' symbols instead.
        await db.Database.ExecuteSqlAsync($"DELETE FROM shared_ai_rounds WHERE stocks::text LIKE {"%" + seeded.Tag + "%"}");
    }

    /// <summary>An answer that names one stock by its name and the other by its symbol.</summary>
    static string Answer(Seeded s, int alpha = 83, int beta = 61) => $"""
        Here you go:

        | Stock | News | Figures | Overall | Reason |
        |---|---|---|---|---|
        | Alpha {s.Tag} | 80 | 85 | {alpha} | Contact me at jane@example.com |
        | BE{s.Tag} | 60 | 62 | {beta} | Quiet quarter |
        """;

    AiRounds Rounds(IServiceScope scope, DateTimeOffset now) =>
        new(scope.ServiceProvider.GetRequiredService<AppDbContext>(), new Clock(now), NullLogger<AiRounds>.Instance);

    [NeonFact]
    public async Task A_simulation_round_gives_the_top_score_a_step_and_buys_it_at_once_at_the_latest_price()
    {
        var s = await StartAsync("simulation");
        try
        {
            var round = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.True(round.GetProperty("open").GetBoolean());
            Assert.False(round.GetProperty("sharingAsked").GetBoolean());
            var prompt = round.GetProperty("prompt").GetString()!;
            Assert.Contains("| Stock | News | Figures | Overall | Reason |", prompt);
            Assert.DoesNotContain("Mood", prompt);

            var dashboard = await s.Client.GetFromJsonAsync<JsonElement>("/dashboard");
            Assert.Contains(dashboard.GetProperty("waiting").EnumerateArray(),
                w => w.GetProperty("kind").GetString() == "aiRound" && w.GetProperty("strategyId").GetGuid() == s.StrategyId);

            var reading = await (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round/read", new { answer = Answer(s) }))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(reading.GetProperty("ok").GetBoolean());
            var top = reading.GetProperty("scores").EnumerateArray().Single(x => x.GetProperty("step").ValueKind == JsonValueKind.Number);
            Assert.Equal((s.Alpha, 83m, 1), (top.GetProperty("assetId").GetGuid(), top.GetProperty("overall").GetDecimal(), top.GetProperty("step").GetInt32()));

            var used = await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s), aiModel = "Claude · Opus", share = true });
            Assert.Equal(HttpStatusCode.OK, used.StatusCode);
            var result = await used.Content.ReadFromJsonAsync<JsonElement>();
            var signalId = Assert.Single(result.GetProperty("signalIds").EnumerateArray()).GetGuid();
            // The next round isn't due yet.
            Assert.Equal(HttpStatusCode.Conflict,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s), aiModel = "Claude · Opus" })).StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var signal = await db.Signals.AsNoTracking().SingleAsync(x => x.Id == signalId);
            Assert.Equal((s.Alpha, SignalStatus.Confirmed, 1, 100m, 100m), (signal.AssetId, signal.Status, signal.StepNo!.Value, signal.StepAmount!.Value, signal.ClosePrice));
            var rounds = await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == s.StrategyId).OrderBy(r => r.DueOn).ToListAsync();
            Assert.Equal([AiRoundStatus.Done, AiRoundStatus.Due], rounds.Select(r => r.Status));
            Assert.Equal(rounds[0].DueOn.AddDays(14), rounds[1].DueOn);
            Assert.Equal("Claude · Opus", rounds[0].AiModel);
            Assert.Contains("jane@example.com", rounds[0].Answer); // The user's own copy is whole.
            var shared = await db.SharedAiRounds.AsNoTracking().SingleAsync(x => x.Answer.Contains($"BE{s.Tag}"));
            Assert.DoesNotContain("jane@example.com", shared.Answer);
            Assert.Equal(["news", "figures"], shared.Sources.Deserialize<string[]>()!);
            Assert.Equal(4, shared.Id.Version); // Random: a version 7 id would carry the time the round was done.
            Assert.True((await db.Users.AsNoTracking().SingleAsync(u => u.Email == s.Email)).AiSharing);

            // Bought at once, at the latest price stored (the test stocks aren't on Yahoo, so the refresh leaves the stored ones).
            var session = UsSession.LastComplete(DateTimeOffset.UtcNow);
            var trade = await db.SignalTrades.AsNoTracking().SingleAsync(t => t.SignalId == signalId);
            Assert.Equal((session, 98.80m, 1.20m, 100m, 0.988m), (trade.TradeDate, trade.Amount, trade.Fee, trade.Price, trade.Shares));

            // The daily check doesn't buy it again.
            var tradeDay = session.AddDays(1);
            db.Prices.Add(new PriceDaily { AssetId = s.Alpha, Date = tradeDay, Open = 110, High = 120, Low = 98, Close = 120, Volume = 1 });
            db.Prices.Add(new PriceDaily { AssetId = s.Beta, Date = tradeDay, Open = 50, High = 50, Low = 50, Close = 50, Volume = 1 });
            await db.SaveChangesAsync();
            var daily = await Rounds(scope, DateTimeOffset.UtcNow).RunDailyAsync(default);
            Assert.Equal((0, 0), (daily.Failed, daily.NewTrades));
            Assert.Single(await db.SignalTrades.AsNoTracking().Where(t => t.StrategyId == s.StrategyId).ToListAsync());

            var page = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}");
            var ai = page.GetProperty("aiRounds");
            Assert.Equal((1, false), (ai.GetProperty("done").GetInt32(), ai.GetProperty("due").GetBoolean()));
            var beta = page.GetProperty("stocks").EnumerateArray().Single(x => x.GetProperty("assetId").GetGuid() == s.Beta);
            Assert.Equal(61m, beta.GetProperty("aiScore").GetDecimal());

            var history = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-rounds");
            var done = history.GetProperty("rounds").EnumerateArray().Single(r => r.GetProperty("status").GetString() == "done");
            Assert.Equal(2, done.GetProperty("scores").GetArrayLength());
            Assert.Equal(20m, done.GetProperty("picks").GetDecimal()); // Alpha's close went from 100 to 120.
            Assert.Equal(0m, done.GetProperty("rest").GetDecimal());
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task A_prompt_changed_since_it_was_copied_is_refused_and_the_copied_one_is_kept()
    {
        var s = await StartAsync("simulation");
        try
        {
            var shown = (await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round")).GetProperty("prompt").GetString()!;
            Assert.Equal(HttpStatusCode.Conflict, (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round",
                new { answer = Answer(s), aiModel = "Claude · Opus", prompt = shown + " (edited elsewhere)" })).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round",
                new { answer = Answer(s), aiModel = "Claude · Opus", prompt = shown })).StatusCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(shown, (await db.AiRounds.AsNoTracking().SingleAsync(r => r.StrategyId == s.StrategyId && r.Status == AiRoundStatus.Done)).PromptText);
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task Turning_sharing_off_removes_the_shared_rounds_which_keep_only_their_day()
    {
        var s = await StartAsync("simulation");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round",
                new { answer = Answer(s), aiModel = "Claude · Opus", share = true })).StatusCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shared = await db.SharedAiRounds.AsNoTracking().SingleAsync(x => x.Answer.Contains($"BE{s.Tag}"));
            Assert.Equal(TimeSpan.Zero, shared.CreatedAt.TimeOfDay); // No time to match against the user's own round.

            Assert.Equal(HttpStatusCode.NoContent, (await s.Client.PostAsJsonAsync("/account/ai-sharing", new { enabled = false })).StatusCode);
            Assert.False(await db.SharedAiRounds.AnyAsync(x => x.Answer.Contains($"BE{s.Tag}")));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task Deleting_an_ai_strategy_removes_its_rounds_and_what_it_shared()
    {
        var s = await StartAsync("simulation");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round",
                new { answer = Answer(s), aiModel = "Claude · Opus", share = true })).StatusCode);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.SharedAiRounds.AnyAsync(x => x.Answer.Contains($"BE{s.Tag}")));

            Assert.Equal(HttpStatusCode.NoContent, (await s.Client.DeleteAsync($"/strategies/{s.StrategyId}")).StatusCode);
            Assert.False(await db.Strategies.AnyAsync(x => x.Id == s.StrategyId));
            Assert.False(await db.AiRounds.AnyAsync(x => x.StrategyId == s.StrategyId));
            Assert.False(await db.Signals.AnyAsync(x => x.StrategyId == s.StrategyId));
            Assert.False(await db.SharedAiRounds.AnyAsync(x => x.Answer.Contains($"BE{s.Tag}")));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task A_strategy_started_before_ai_rounds_existed_starts_from_today_without_skipped_rounds()
    {
        var s = await StartAsync("simulation");
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AiRounds.Where(r => r.StrategyId == s.StrategyId).ExecuteDeleteAsync();
                await db.Strategies.Where(x => x.Id == s.StrategyId).ExecuteUpdateAsync(x => x.SetProperty(y => y.StartedOn, y => y.StartedOn.AddDays(-90)));
            }

            var round = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.True(round.GetProperty("open").GetBoolean());
            Assert.Equal(round.GetProperty("today").GetString(), round.GetProperty("dueOn").GetString());
            var history = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-rounds");
            Assert.Equal(["due"], history.GetProperty("rounds").EnumerateArray().Select(r => r.GetProperty("status").GetString()));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task A_stock_tied_with_the_top_score_is_the_top_score_whichever_is_picked()
    {
        var s = await StartAsync("live");
        try
        {
            var result = await (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s, alpha: 80, beta: 80), aiModel = "Claude · Opus" }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var signalId = Assert.Single(result.GetProperty("signalIds").EnumerateArray()).GetGuid();
            var signal = await s.Client.GetFromJsonAsync<JsonElement>($"/signals/{signalId}");
            Assert.EndsWith("the top score of the round", signal.GetProperty("reason").GetString());
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task An_ended_strategy_has_no_round_due()
    {
        var s = await StartAsync("simulation");
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Strategies.Where(x => x.Id == s.StrategyId).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, StrategyStatus.Ended)
                    .SetProperty(y => y.EndedAt, DateTimeOffset.UtcNow));
            }

            var page = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}");
            Assert.False(page.GetProperty("aiRounds").GetProperty("due").GetBoolean());
            Assert.Equal(HttpStatusCode.NotFound, (await s.Client.GetAsync($"/strategies/{s.StrategyId}/ai-round")).StatusCode);
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task A_live_round_waits_for_the_user_and_is_missed_when_the_next_round_is_due()
    {
        var s = await StartAsync("live");
        try
        {
            var result = await (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s, alpha: 40, beta: 90), aiModel = "Other model" }))
                .Content.ReadFromJsonAsync<JsonElement>();
            var signalId = Assert.Single(result.GetProperty("signalIds").EnumerateArray()).GetGuid();

            var signal = await s.Client.GetFromJsonAsync<JsonElement>($"/signals/{signalId}");
            Assert.Equal("waiting", signal.GetProperty("status").GetString());
            Assert.Equal("your AI scored it 90 out of 100, the top score of the round", signal.GetProperty("reason").GetString());

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False((await db.Users.AsNoTracking().SingleAsync(u => u.Email == s.Email)).AiSharing);
            await Rounds(scope, DateTimeOffset.UtcNow.AddDays(15)).RunDailyAsync(default);
            var missed = await db.Signals.AsNoTracking().SingleAsync(x => x.Id == signalId);
            Assert.Equal(SignalStatus.Missed, missed.Status);
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task A_round_not_run_within_its_week_is_skipped_and_the_next_one_comes_two_weeks_after_its_due_date()
    {
        var s = await StartAsync("simulation");
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var round = await db.AiRounds.SingleAsync(r => r.StrategyId == s.StrategyId);
                round.DueOn = round.DueOn.AddDays(-10);
                await db.SaveChangesAsync();
            }

            var current = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.False(current.GetProperty("open").GetBoolean());
            var history = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-rounds");
            Assert.Equal(["due", "skipped"], history.GetProperty("rounds").EnumerateArray().Select(r => r.GetProperty("status").GetString()));
            var dueOn = DateOnly.Parse(current.GetProperty("dueOn").GetString()!);
            var skippedOn = DateOnly.Parse(history.GetProperty("rounds")[1].GetProperty("dueOn").GetString()!);
            Assert.Equal(skippedOn.AddDays(14), dueOn);

            Assert.Equal(HttpStatusCode.Conflict,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s), aiModel = "Claude · Opus" })).StatusCode);
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public Task A_due_simulation_round_can_be_skipped_and_the_next_one_comes_when_scheduled() => SkipsAsync("simulation");

    [NeonFact]
    public Task A_due_live_round_can_be_skipped_and_the_next_one_comes_when_scheduled() => SkipsAsync("live");

    async Task SkipsAsync(string mode)
    {
        var s = await StartAsync(mode);
        try
        {
            var round = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            var roundId = round.GetProperty("roundId").GetGuid();
            var dueOn = DateOnly.Parse(round.GetProperty("dueOn").GetString()!);
            // A stale screen can't skip another round.
            Assert.Equal(HttpStatusCode.Conflict,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round/skip", new { roundId = Guid.NewGuid() })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round/skip", new { roundId })).StatusCode);
            // The next isn't due yet: a second tap skips nothing.
            Assert.Equal(HttpStatusCode.Conflict,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round/skip", new { roundId })).StatusCode);

            var next = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.False(next.GetProperty("open").GetBoolean());
            Assert.Equal(dueOn.AddDays(14), DateOnly.Parse(next.GetProperty("dueOn").GetString()!));
            var history = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-rounds");
            Assert.Equal(["due", "skipped"], history.GetProperty("rounds").EnumerateArray().Select(r => r.GetProperty("status").GetString()));
            using var scope = factory.Services.CreateScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Signals.AnyAsync(x => x.StrategyId == s.StrategyId));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task An_extra_round_can_be_run_before_the_next_is_due_once_a_day_without_moving_the_schedule()
    {
        var s = await StartAsync("simulation");
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round/skip", new { })).StatusCode);
            var scheduled = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.False(scheduled.GetProperty("open").GetBoolean());

            // Not on purpose (a second tap after a round): refused.
            Assert.Equal(HttpStatusCode.Conflict,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s), aiModel = "Claude · Opus" })).StatusCode);
            var used = await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s), aiModel = "Claude · Opus", extra = true });
            Assert.Equal(HttpStatusCode.OK, used.StatusCode);
            var signalId = Assert.Single((await used.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("signalIds").EnumerateArray()).GetGuid();
            Assert.Equal(HttpStatusCode.Conflict,
                (await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = Answer(s), aiModel = "Claude · Opus", extra = true })).StatusCode);

            var after = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.Equal((scheduled.GetProperty("roundId").GetGuid(), scheduled.GetProperty("dueOn").GetString()),
                (after.GetProperty("roundId").GetGuid(), after.GetProperty("dueOn").GetString()));
            var history = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-rounds");
            var extra = Assert.Single(history.GetProperty("rounds").EnumerateArray(), r => r.GetProperty("extra").GetBoolean());
            Assert.Equal(("done", after.GetProperty("today").GetString()), (extra.GetProperty("status").GetString(), extra.GetProperty("dueOn").GetString()));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var signal = await db.Signals.AsNoTracking().SingleAsync(x => x.Id == signalId);
            // Its time is up when the scheduled round is due.
            Assert.Equal(after.GetProperty("dueOn").GetString(), signal.Facts.RootElement.GetProperty("endsOn").GetString());
            Assert.True(await db.SignalTrades.AnyAsync(t => t.SignalId == signalId));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task An_unreadable_answer_or_no_model_is_refused_and_saves_nothing()
    {
        var s = await StartAsync("simulation");
        try
        {
            var refused = await s.Client.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round", new { answer = "I can't help with that.", aiModel = "" });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var errors = (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.True(errors.TryGetProperty("answer", out _));
            Assert.True(errors.TryGetProperty("aiModel", out _));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(AiRoundStatus.Due, (await db.AiRounds.AsNoTracking().SingleAsync(r => r.StrategyId == s.StrategyId)).Status);
            Assert.False(await db.Signals.AnyAsync(x => x.StrategyId == s.StrategyId));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task The_prompt_choices_are_kept_and_tuning_the_settings_leaves_them_alone()
    {
        var s = await StartAsync("simulation", ai: new { news = true, mood = true, figures = false, instructions = "Score each stock on its AI products." },
            settings: new { topN = 2 });
        try
        {
            Assert.Equal(HttpStatusCode.NoContent,
                (await s.Client.PutAsJsonAsync($"/strategies/{s.StrategyId}/settings", new { settings = new { everyDays = 10, topN = 2 } })).StatusCode);
            var round = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.True(round.GetProperty("promptEdited").GetBoolean());
            Assert.Contains("Score each stock on its AI products.", round.GetProperty("prompt").GetString());
            Assert.Contains("last 10 days", round.GetProperty("prompt").GetString());
            Assert.Equal(10, round.GetProperty("everyDays").GetInt32());
            Assert.Equal(2, round.GetProperty("topN").GetInt32());

            Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PutAsJsonAsync($"/strategies/{s.StrategyId}/ai-prompt",
                new { news = false, mood = false, figures = false, instructions = (string?)null })).StatusCode);
            // The suggested words, sent back as they are, count as not edited.
            var suggested = round.GetProperty("suggested").GetString();
            Assert.Equal(HttpStatusCode.NoContent, (await s.Client.PutAsJsonAsync($"/strategies/{s.StrategyId}/ai-prompt",
                new { news = true, mood = true, figures = false, instructions = suggested })).StatusCode);
            round = await s.Client.GetFromJsonAsync<JsonElement>($"/strategies/{s.StrategyId}/ai-round");
            Assert.False(round.GetProperty("promptEdited").GetBoolean());
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task Another_users_round_is_not_found_and_a_strategy_needs_one_source()
    {
        var s = await StartAsync("simulation");
        try
        {
            var (other, _) = await factory.SignedUpAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/strategies/{s.StrategyId}/ai-round")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await other.PostAsJsonAsync($"/strategies/{s.StrategyId}/ai-round/read", new { answer = Answer(s) })).StatusCode);

            var refused = await s.Client.PostAsJsonAsync("/strategies", new
            {
                templateKey = "ai", name = "No sources", mode = "simulation", budget = new { amount = 3000, step = 100, maxSteps = 5 },
                assetIds = new[] { s.Alpha }, ai = new { news = false, mood = false, figures = false },
            });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.True((await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("ai.sources", out _));
        }
        finally
        {
            await CleanAsync(s);
        }
    }

    [NeonFact]
    public async Task The_round_due_email_goes_out_once_from_seven_in_the_morning_but_not_for_the_first_round()
    {
        var s = await StartAsync("simulation");
        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(u => u.Email == s.Email).ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailVerifiedAt, DateTimeOffset.UtcNow));
            var sender = new Captured();
            var service = ActivatorUtilities.CreateInstance<NotificationService>(scope.ServiceProvider, (IEmailSender)sender);

            var round = await db.AiRounds.AsNoTracking().SingleAsync(r => r.StrategyId == s.StrategyId);
            var morning = new DateTimeOffset(round.DueOn.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero); // 10:00 or 11:00 in Madrid.
            await service.SendAiRoundRemindersAsync(morning, default);
            Assert.DoesNotContain(sender.Sent, m => m.To == s.Email); // The first round is due the day it starts.

            var later = round.DueOn.AddDays(14);
            await db.AiRounds.Where(r => r.Id == round.Id).ExecuteUpdateAsync(r => r.SetProperty(x => x.DueOn, later));
            await service.SendAiRoundRemindersAsync(new DateTimeOffset(later.ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero), default);
            Assert.DoesNotContain(sender.Sent, m => m.To == s.Email); // Before 07:00 in Madrid.
            await service.SendAiRoundRemindersAsync(new DateTimeOffset(later.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero), default);
            await service.SendAiRoundRemindersAsync(new DateTimeOffset(later.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero), default);

            var mail = Assert.Single(sender.Sent, m => m.To == s.Email);
            Assert.StartsWith("Time for the AI round of AI ", mail.Subject);
            Assert.Contains($"/strategies/{s.StrategyId}/ai-round", mail.Body);
        }
        finally
        {
            await CleanAsync(s);
        }
    }
}
