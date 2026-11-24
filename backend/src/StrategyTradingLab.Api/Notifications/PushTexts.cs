using System.Text.Json;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// What a push notification carries, as the service worker reads it (frontend <c>public/push-sw.js</c>): a title, the text,
/// the app path it opens, and a tag, so a second notification about the same thing replaces the first.
/// </summary>
public record PushMessage(string Title, string Body, string Url, string Tag)
{
    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);
}

/// <summary>The words of the push notifications: the email's subject as the title and the signal box as the text, or the stock change line (spec: Notifications).</summary>
public static class PushTexts
{
    /// <summary>The longest title and text kept (<c>notifications.title</c>, <c>body</c>), and allowed for a test push.</summary>
    public const int TitleMax = 200, BodyMax = 1000;

    /// <summary>A test push from /admin/notifications: it opens the dashboard, and each one shows on its own (its own tag).</summary>
    public static PushMessage Test(Guid notificationId, string title, string body) => new(title, body, "/", $"test-{notificationId}");

    public static PushMessage SignalAlert(SignalLine s, string locale) => new(
        EmailTexts.AlertTitle(s, locale), EmailTexts.SignalText(s, locale), SignalPath(s), $"signal-{s.SignalId}");

    public static PushMessage SnoozeReminder(SignalLine s, string locale) => new(
        EmailTexts.SnoozeTitle(s, locale), EmailTexts.SignalText(s, locale), SignalPath(s), $"signal-{s.SignalId}");

    /// <summary>Replaces the signal's own notification on the device (the same tag).</summary>
    public static PushMessage SignalEnded(SignalLine s, DateOnly endedOn, string locale) => new(
        EmailTexts.EndedTitle(s, locale), EmailTexts.EndedText(s, endedOn, locale), SignalPath(s), $"signal-{s.SignalId}");

    /// <summary>One for every round due that day; it opens the round when there is one, else the dashboard.</summary>
    public static PushMessage AiRoundDue(IReadOnlyList<(Guid StrategyId, string Name)> strategies, DateOnly dueOn, string locale)
    {
        return new(
            EmailTexts.AiRoundTitle(locale),
            EmailTexts.AiRoundIntro(strategies, locale),
            strategies.Count == 1 ? $"/strategies/{strategies[0].StrategyId}/ai-round" : "/",
            $"ai-round-{dueOn:yyyy-MM-dd}");
    }

    /// <summary>
    /// A stock that became a candidate or came within the getting-close % of its line: "Getting close: Tesla" or "New candidate:
    /// Apple", with the stock change line and the strategy's mode. It opens the stock's page; a later change of the same stock
    /// replaces it on the device (the same tag).
    /// </summary>
    public static PushMessage StockChange(StockChangeLine c, string locale)
    {
        var title = (c.Kind == StockChangeKind.CameClose ? CameClose : NewCandidate).In(locale, c.StockName);
        return new(title, $"{EmailTexts.StockChangeText(c, locale)} ({EmailTexts.ModeName(c.Mode, locale)})",
            $"/strategies/{c.StrategyId}/stocks/{c.AssetId}", $"stock-{c.StrategyId}-{c.AssetId}");
    }

    static readonly Message CameClose = new("Getting close: {0}", "A prop de la línia: {0}");
    static readonly Message NewCandidate = new("New candidate: {0}", "Nova candidata: {0}");

    static string SignalPath(SignalLine s) => $"/strategies/{s.StrategyId}/signals/{s.SignalId}";
}
