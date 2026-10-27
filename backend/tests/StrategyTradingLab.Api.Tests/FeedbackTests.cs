using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Feedback;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace StrategyTradingLab.Api.Tests;

public class FeedbackValidationTests
{
    [Theory]
    [InlineData("/dashboard", true)]
    [InlineData("/new/templates/dip?x=1", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//evil.example/x", false)]
    [InlineData("/a/..//evil.example", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("dashboard", false)]
    [InlineData(@"/\evil.example", false)]
    [InlineData("/\t/evil.example", false)]
    [InlineData("/a b", false)]
    public void Route_must_be_a_path_in_the_app(string route, bool valid)
    {
        var errors = FeedbackEndpoints.ValidateCreate(new(FeedbackKind.Note, "A comment", Route: route), out _, out _, out _, out _);

        Assert.Equal(valid, !errors.ContainsKey("route"));
    }

    [Fact]
    public void Create_requires_a_known_kind_and_non_blank_comment_within_limits()
    {
        Assert.Contains("kind", FeedbackEndpoints.ValidateCreate(new(null, "A comment"), out _, out _, out _, out _).Keys);
        Assert.Contains("kind", FeedbackEndpoints.ValidateCreate(new((FeedbackKind)99, "A comment"), out _, out _, out _, out _).Keys);
        Assert.Contains("text", FeedbackEndpoints.ValidateCreate(new(FeedbackKind.Note, "   "), out _, out _, out _, out _).Keys);
        Assert.Contains("text", FeedbackEndpoints.ValidateCreate(new(FeedbackKind.Note, new string('x', 4001)), out _, out _, out _, out _).Keys);
        Assert.Contains("element", FeedbackEndpoints.ValidateCreate(new(FeedbackKind.Note, "ok", Element: new string('x', 301)), out _, out _, out _, out _).Keys);
        Assert.Contains("selectedText", FeedbackEndpoints.ValidateCreate(new(FeedbackKind.Note, "ok", SelectedText: new string('x', 2001)), out _, out _, out _, out _).Keys);
    }

    [Fact]
    public void Blank_place_fields_normalize_to_null_and_text_is_trimmed()
    {
        var errors = FeedbackEndpoints.ValidateCreate(
            new(FeedbackKind.Idea, "  Dark chart  ", Route: "  ", Element: "", SelectedText: " \n "),
            out var text, out var route, out var element, out var selectedText);

        Assert.Empty(errors);
        Assert.Equal("Dark chart", text);
        Assert.Null(route);
        Assert.Null(element);
        Assert.Null(selectedText);
    }

    static IConfiguration Config(string? owner) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["OWNER_EMAIL"] = owner, ["PublicUrl"] = "https://stl.example/" })
        .Build();

    static FeedbackItem Item(string? route = null, string? selectedText = null) => new()
    {
        Id = Guid.NewGuid(), Kind = FeedbackKind.Bug, Status = FeedbackStatus.Open, Text = "The button does nothing",
        Route = route, SelectedText = selectedText,
    };

    [Fact]
    public void The_owner_gets_an_email_that_answers_the_sender()
    {
        var email = FeedbackEndpoints.OwnerEmail(Item("/new/templates/ai", "ADD A STOCK"), "ana@example.com", Config(" owner@example.com ")) ?? throw new Xunit.Sdk.XunitException("No email.");

        Assert.Equal("owner@example.com", email.To);
        Assert.Equal("ana@example.com", email.ReplyTo);
        Assert.Equal("New feedback (bug)", email.Subject);
        Assert.Contains("ana@example.com sent feedback (bug) on /new/templates/ai.", email.Body);
        Assert.Contains("About: \"ADD A STOCK\"", email.Body);
        Assert.Contains("The button does nothing", email.Body);
        Assert.Contains("https://stl.example/feedback", email.Body);
    }

    [Fact]
    public void Feedback_from_the_account_page_has_no_page_or_quote()
    {
        var email = FeedbackEndpoints.OwnerEmail(Item(), "ana@example.com", Config("owner@example.com"))!;

        Assert.StartsWith("ana@example.com sent feedback (bug).\n\nThe button does nothing", email.Body);
        Assert.DoesNotContain("About:", email.Body);
    }

    [Fact]
    public void No_email_without_an_owner_address_or_for_the_owners_own_feedback()
    {
        Assert.Null(FeedbackEndpoints.OwnerEmail(Item(), "ana@example.com", Config(null)));
        Assert.Null(FeedbackEndpoints.OwnerEmail(Item(), "Owner@Example.com", Config("owner@example.com")));
    }

    [Fact]
    public void Update_rejects_blank_or_oversized_text()
    {
        Assert.Contains("text", FeedbackEndpoints.ValidateUpdate(new(Text: "   "), out _).Keys);
        Assert.Contains("text", FeedbackEndpoints.ValidateUpdate(new(Text: new string('x', 4001)), out _).Keys);
        Assert.Empty(FeedbackEndpoints.ValidateUpdate(new(Status: FeedbackStatus.Closed), out var text));
        Assert.Null(text);
    }
}

public class FeedbackEndpointTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    [NeonFact]
    public async Task Feedback_requires_a_log_in_and_submitting_requires_a_confirmed_email()
    {
        using var stranger = api.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/feedback", new { kind = "note", text = "hi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/feedback")).StatusCode);

        using var unverified = (await api.SignedUpAsync()).Client;
        Assert.Equal(HttpStatusCode.Forbidden, (await unverified.PostAsJsonAsync("/feedback", new { kind = "note", text = "hi" })).StatusCode);

        using var unverifiedVip = await api.SignedUpAsync(UserRole.Vip);
        Assert.Equal(HttpStatusCode.Forbidden, (await unverifiedVip.PostAsJsonAsync("/feedback", new { kind = "note", text = "hi" })).StatusCode);
    }

    [NeonFact]
    public async Task Any_verified_user_can_submit_feedback_and_only_the_owner_can_review_it()
    {
        var (user, userEmail) = await api.VerifiedAsync(UserRole.User);
        var (vip, vipEmail) = await api.VerifiedAsync(UserRole.Vip);
        using var owner = await api.OwnerAsync();

        var fromSelection = await user.PostAsJsonAsync("/feedback", new
        {
            kind = "bug",
            text = "  Use plainer words  ",
            route = "/new/templates/dip",
            element = "main > section > p",
            selectedText = "8% below its recent peak",
        });
        Assert.Equal(HttpStatusCode.Created, fromSelection.StatusCode);
        var created = await fromSelection.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(userEmail, created.GetProperty("userEmail").GetString());
        Assert.Equal("bug", created.GetProperty("kind").GetString());
        Assert.Equal("open", created.GetProperty("status").GetString());
        Assert.Equal("Use plainer words", created.GetProperty("text").GetString());
        Assert.Equal("/new/templates/dip", created.GetProperty("route").GetString());
        Assert.Equal("main > section > p", created.GetProperty("element").GetString());
        Assert.Equal("8% below its recent peak", created.GetProperty("selectedText").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("closedAt").ValueKind);

        var fromVip = await vip.PostAsJsonAsync("/feedback", new { kind = "idea", text = "Dark chart", route = "  ", selectedText = "" });
        Assert.Equal(HttpStatusCode.Created, fromVip.StatusCode);
        var second = await fromVip.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = second.GetProperty("id").GetGuid();
        Assert.Equal(vipEmail, second.GetProperty("userEmail").GetString());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("route").ValueKind);

        // Neither user nor vip may list, edit, close or delete feedback.
        foreach (var caller in new[] { user, vip })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.GetAsync("/feedback")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.PatchAsJsonAsync($"/feedback/{id}", new { status = "closed" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.DeleteAsync($"/feedback/{id}")).StatusCode);
        }

        // The owner sees both, newest first, and can edit, close, filter, reopen and delete.
        var all = (await owner.GetFromJsonAsync<JsonElement>("/feedback")).EnumerateArray().ToList();
        Assert.Contains(all, f => f.GetProperty("id").GetGuid() == id);
        Assert.Contains(all, f => f.GetProperty("id").GetGuid() == secondId);

        var patched = await (await owner.PatchAsJsonAsync($"/feedback/{id}", new { kind = "feature", text = "Better copy" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("feature", patched.GetProperty("kind").GetString());
        Assert.Equal("Better copy", patched.GetProperty("text").GetString());
        Assert.Equal("/new/templates/dip", patched.GetProperty("route").GetString());

        var closed = await (await owner.PatchAsJsonAsync($"/feedback/{id}", new { status = "closed" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("closed", closed.GetProperty("status").GetString());
        var closedAt = closed.GetProperty("closedAt").GetDateTimeOffset();

        // Closing again keeps the first closing time.
        var closedAgain = await (await owner.PatchAsJsonAsync($"/feedback/{id}", new { status = "closed" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(closedAt, closedAgain.GetProperty("closedAt").GetDateTimeOffset(), TimeSpan.FromMicroseconds(1));

        var openList = (await owner.GetFromJsonAsync<JsonElement>("/feedback?status=open")).EnumerateArray().ToList();
        Assert.DoesNotContain(openList, f => f.GetProperty("id").GetGuid() == id);
        Assert.Contains(openList, f => f.GetProperty("id").GetGuid() == secondId);

        var closedList = (await owner.GetFromJsonAsync<JsonElement>("/feedback?status=closed")).EnumerateArray().ToList();
        Assert.Contains(closedList, f => f.GetProperty("id").GetGuid() == id);

        var reopened = await (await owner.PatchAsJsonAsync($"/feedback/{id}", new { status = "open" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("open", reopened.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("closedAt").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/feedback/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/feedback/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PatchAsJsonAsync($"/feedback/{id}", new { status = "closed" })).StatusCode);
    }

    [NeonFact]
    public async Task New_feedback_is_emailed_to_the_owner_with_answers_going_to_the_sender()
    {
        using var withOwner = api.WithWebHostBuilder(b => b.UseSetting("OWNER_EMAIL", "owner-inbox@example.test"));
        var (user, userEmail) = await api.VerifiedAsync(UserRole.User, withOwner);

        var response = await user.PostAsJsonAsync("/feedback", new { kind = "feature", text = "Let me change the budget" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var email = await api.Emails.WaitForAsync("owner-inbox@example.test", "New feedback (feature)");
        Assert.NotNull(email);
        Assert.Equal(userEmail, email.ReplyTo);
        Assert.Contains("Let me change the budget", email.Body);
    }
}
