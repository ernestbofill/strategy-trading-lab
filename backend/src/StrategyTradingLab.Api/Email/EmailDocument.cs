using System.Net;
using System.Text;

namespace StrategyTradingLab.Api.Email;

/// <summary>An email ready to send: the plain text and the same content as HTML (spec: Emails).</summary>
/// <param name="OneClickUnsubscribe">The address that turns this kind of email off with one POST (the List-Unsubscribe header), when the email has one.</param>
public record EmailContent(string Subject, string Text, string Html, string? OneClickUnsubscribe = null)
{
    public void Deconstruct(out string subject, out string text, out string html) => (subject, text, html) = (Subject, Text, Html);
}

/// <summary>The kinds of email a user can turn off from the email itself.</summary>
public enum EmailKind { Overview, Alerts }

/// <summary>The addresses that turn one kind of email off for the user whose private token is in them (see <c>User.UnsubscribeToken</c>).</summary>
public static class UnsubscribeLinks
{
    public static string Slug(EmailKind kind) => kind == EmailKind.Overview ? "overview" : "alerts";

    public static EmailKind? FromSlug(string slug) => slug switch { "overview" => EmailKind.Overview, "alerts" => EmailKind.Alerts, _ => null };

    /// <summary>The app's page, with one button: opening a link must never change anything, because mail scanners open every link.</summary>
    public static string PageUrl(string appUrl, string locale, string token, EmailKind kind) => AppLink.To(appUrl, locale, $"/unsubscribe/{token}/{Slug(kind)}");

    /// <summary>The API's address for the List-Unsubscribe header: mail apps POST to it (RFC 8058).</summary>
    public static string OneClickUrl(string appUrl, string token, EmailKind kind) => $"{appUrl}/api/email/unsubscribe/{token}/{Slug(kind)}";
}

/// <summary>A line in a section; <paramref name="Url"/> links it to its page, <paramref name="Tag"/> is a mode label (Live tags stand out).</summary>
public record EmailItem(string Text, string? Url = null, string? Tag = null, bool Live = false);

/// <summary>
/// The app's one email layout, in the cyberpunk style of the app. An email is a list of blocks, and each block writes both its
/// plain text and its HTML, so the two parts of an email always say the same thing. Pure: blocks in, text and HTML out.
/// HTML for email: tables and inline styles only (clients drop &lt;style&gt; and most CSS), every user value encoded.
/// </summary>
public sealed class EmailDocument(string locale = "en", string? appUrl = null)
{
    // The app's tokens (design-system/cyberpunk, frontend/src/styles/tokens.css).
    const string Ground = "#100d23", Card = "#261d45", Hairline = "#372963", Ink = "#ffffff", InkSoft = "#b08bff",
        Primary = "#00ff9c", Accent = "#00ffc8", OnAction = "#00140b", HotPink = "#ff4081", Info = "#00c3ff";
    const string Mono = "'Fira Code',Consolas,'Courier New',monospace";
    const string Ui = "system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    readonly List<(string Text, string Html)> blocks = [];
    string? eyebrow, disclaimer, preheader;
    (string Label, string PageUrl, string OneClickUrl)? unsubscribe;

    bool Ca => locale == "ca";

    static string Encode(string text) => WebUtility.HtmlEncode(text);

    /// <summary>Encoded, with line breaks kept.</summary>
    static string Lines(string text) => Encode(text).Replace("\n", "<br>");

    EmailDocument Add(string text, string html)
    {
        blocks.Add((text, html));
        return this;
    }

    /// <summary>The small uppercase label above the content (HTML only), e.g. "Signal · Manual".</summary>
    public EmailDocument Eyebrow(string text)
    {
        eyebrow = text;
        return this;
    }

    public EmailDocument Heading(string text)
    {
        preheader ??= text;
        return Add(text, $"<h1 style=\"margin:0 0 16px;font-family:{Mono};font-size:22px;line-height:30px;font-weight:700;color:{Ink};\">{Encode(text.TrimEnd('.'))}</h1>");
    }

    public EmailDocument Paragraph(string text)
    {
        preheader ??= text;
        return Add(text, $"<p style=\"margin:0 0 16px;\">{Lines(text)}</p>");
    }

    /// <summary>The signal box: its first line as text, the lines after it (the close) as figures.</summary>
    public EmailDocument Box(string text)
    {
        preheader ??= text;
        var lines = text.Split('\n');
        var figures = string.Concat(lines.Skip(1).Select(line =>
            $"<div style=\"margin-top:8px;font-family:{Mono};font-size:14px;line-height:20px;color:{Accent};\">{Encode(line)}</div>"));
        return Add(text,
            $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 20px;\"><tr>" +
            $"<td bgcolor=\"{Ground}\" style=\"background:{Ground};border-left:3px solid {Primary};padding:14px 16px;\">{Encode(lines[0])}{figures}</td></tr></table>");
    }

    /// <summary>A code chip (e.g. an invite code), with an optional hint line.</summary>
    public EmailDocument Code(string label, string code, string? hint = null)
    {
        var text = hint is null ? $"{label}: {code}" : $"{label}: {code}\n{hint}";
        var hintHtml = hint is null ? "" : $"<p style=\"margin:8px 0 0;font-size:13px;line-height:18px;color:{InkSoft};\">{Lines(hint)}</p>";
        return Add(text,
            $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 20px;\"><tr>" +
            $"<td bgcolor=\"{Ground}\" style=\"background:{Ground};border:1px dashed {Hairline};padding:14px 16px;text-align:center;\">" +
            $"<div style=\"font-family:{Mono};font-size:11px;line-height:16px;letter-spacing:1.5px;text-transform:uppercase;color:{InkSoft};margin-bottom:4px;\">{Encode(label)}</div>" +
            $"<div style=\"font-family:{Mono};font-size:20px;line-height:26px;font-weight:700;letter-spacing:3px;color:{Primary};\">{Encode(code)}</div>" +
            $"{hintHtml}</td></tr></table>");
    }

    public EmailDocument Quote(string label, string body) =>
        Add($"{label}: \"{body}\"",
            $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 16px;\"><tr>" +
            $"<td bgcolor=\"{Ground}\" style=\"background:{Ground};border-left:3px solid {Hairline};padding:10px 14px;font-size:14px;line-height:20px;color:{InkSoft};\">" +
            $"<strong style=\"color:{Ink};\">{Encode(label)}:</strong> &ldquo;{Lines(body)}&rdquo;</td></tr></table>");

    /// <summary>The email's call to action: a button in the HTML, a "lead url" in the text.</summary>
    public EmailDocument Action(string button, string url) => Action(null, button, url);

    /// <summary>A call to action with a lead sentence ("See it:", "Confirm by opening:").</summary>
    public EmailDocument Action(string? lead, string button, string url)
    {
        if (lead is not null) preheader ??= lead;
        var leadText = lead is null ? url : $"{lead}\n{url}";
        var leadHtml = lead is null ? "" : $"<p style=\"margin:0 0 12px;\">{Encode(lead.TrimEnd(':'))}.</p>";
        return Add(leadText,
            leadHtml +
            $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 20px;\"><tr>" +
            $"<td bgcolor=\"{Primary}\" style=\"background:{Primary};border-radius:2px;\">" +
            $"<a href=\"{Encode(url)}\" style=\"display:inline-block;padding:12px 20px;font-family:{Mono};font-size:14px;line-height:20px;font-weight:700;" +
            $"letter-spacing:0.5px;text-transform:uppercase;color:{OnAction};text-decoration:none;\">{Encode(button)}</a></td></tr></table>");
    }

    /// <summary>Links side by side: one "label: url" line each in the text, outlined buttons in the HTML.</summary>
    public EmailDocument Links(IEnumerable<(string Label, string Url)> links)
    {
        var list = links.ToList();
        return Add(string.Join("\n", list.Select(l => $"{l.Label}: {l.Url}")),
            "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 12px;\">" +
            string.Concat(list.Select(l =>
                $"<tr><td style=\"padding:0 0 8px;\"><a href=\"{Encode(l.Url)}\" style=\"display:inline-block;padding:11px 18px;border:1px solid {Primary};" +
                $"border-radius:2px;font-family:{Mono};font-size:14px;line-height:20px;font-weight:700;color:{Primary};text-decoration:none;\">" +
                $"{Encode(l.Label)} &rarr;</a></td></tr>")) +
            "</table>");
    }

    /// <summary>A titled list ("Waiting for you"): "- " lines in the text, rows with mode tags and links in the HTML.</summary>
    public EmailDocument Section(string title, IEnumerable<EmailItem> items, string? url = null)
    {
        var list = items.ToList();
        var text = new StringBuilder(url is null ? title : $"{title}: {url}");
        foreach (var item in list)
        {
            var tag = item.Tag is null ? "" : $"({item.Tag}) ";
            var itemUrl = item.Url is null ? "" : $" {item.Url}";
            text.Append($"\n- {tag}{item.Text}{itemUrl}");
        }

        var titleHtml = url is null
            ? Encode(title)
            : $"<a href=\"{Encode(url)}\" style=\"color:{Accent};text-decoration:underline;\">{Encode(title)}</a>";

        var html = new StringBuilder(
            $"<p style=\"margin:20px 0 8px;font-family:{Mono};font-size:13px;line-height:18px;font-weight:700;letter-spacing:1px;text-transform:uppercase;color:{Accent};\">" +
            $"{titleHtml}</p>" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 16px;\">");
        foreach (var item in list)
        {
            var tag = item.Tag is null ? ""
                : $"<span style=\"display:inline-block;padding:1px 6px;margin-right:8px;font-family:{Mono};font-size:11px;line-height:16px;font-weight:700;" +
                  $"background:{(item.Live ? HotPink : Hairline)};color:{Ink};border-radius:2px;\">{Encode(item.Tag)}</span> ";
            var body = item.Url is null ? Encode(item.Text)
                : $"<a href=\"{Encode(item.Url)}\" style=\"color:{Primary};text-decoration:underline;\">{Encode(item.Text)}</a>";
            html.Append($"<tr><td style=\"padding:10px 0;border-bottom:1px solid {Hairline};font-size:15px;line-height:22px;\">{tag}{body}</td></tr>");
        }
        html.Append("</table>");
        return Add(text.ToString(), html.ToString());
    }

    /// <summary>The disclaimer: the last line of the text, set apart at the foot of the card in the HTML.</summary>
    public EmailDocument Disclaimer(string text)
    {
        disclaimer = text;
        return this;
    }

    /// <summary>"Turn off these emails": a line above the disclaimer in the text, a link in the footer in the HTML, and the one-click address for the header.</summary>
    public EmailDocument Unsubscribe(EmailKind kind, string token)
    {
        if (appUrl is null) throw new InvalidOperationException("An unsubscribe link needs the app's address.");
        var label = (kind, Ca) switch
        {
            (EmailKind.Overview, false) => "Turn off summary emails",
            (EmailKind.Overview, true) => "Desactiva els mails de resum",
            (_, false) => "Turn off signal and reminder emails",
            _ => "Desactiva els mails de senyals i recordatoris",
        };
        unsubscribe = (label, UnsubscribeLinks.PageUrl(appUrl, locale, token, kind), UnsubscribeLinks.OneClickUrl(appUrl, token, kind));
        return this;
    }

    /// <summary><see cref="Unsubscribe(EmailKind, string)"/> when there is a token (an email to an address without an account has none).</summary>
    public EmailDocument WithUnsubscribe(EmailKind kind, string? token) => token is null ? this : Unsubscribe(kind, token);

    public EmailContent Build(string subject)
    {
        var text = string.Join("\n\n", blocks.Select(b => b.Text)
            .Append(unsubscribe is { } u ? $"{u.Label}: {u.PageUrl}" : null).Append(disclaimer).OfType<string>());
        return new(subject, text, Html(subject), unsubscribe?.OneClickUrl);
    }

    string Html(string subject)
    {
        var lang = Ca ? "ca" : "en";
        var line = preheader?.Replace('\n', ' ') ?? "";
        var cut = line.Length > 140 && char.IsHighSurrogate(line[138]) ? 138 : 139; // Never half an emoji.
        var preview = Encode(line.Length > 140 ? line[..cut] + "…" : line);
        var home = appUrl is null ? null : Encode(AppLink.To(appUrl, locale));
        var brand = home is null ? "STRATEGY TRADING LAB"
            : $"<a href=\"{home}\" style=\"color:{Primary};text-decoration:none;\">STRATEGY TRADING LAB</a>";
        // No "you get this because you have an account": a sign-up confirmation can reach someone who never signed up.
        var footer = "Strategy Trading Lab" + (home is null ? ""
            : $" &middot; <a href=\"{home}\" style=\"color:{InkSoft};\">{Encode(Ca ? "Obre l'aplicació" : "Open the app")}</a>")
            + (unsubscribe is { } u ? $" &middot; <a href=\"{Encode(u.PageUrl)}\" style=\"color:{InkSoft};\">{Encode(u.Label)}</a>" : "");

        return
            $"<!doctype html><html lang=\"{lang}\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
            "<meta name=\"color-scheme\" content=\"dark\"><meta name=\"supported-color-schemes\" content=\"dark\">" +
            $"<title>{Encode(subject)}</title></head>" +
            $"<body style=\"margin:0;padding:0;background:{Ground};\">" +
            $"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;color:{Ground};\">{preview}</div>" +
            $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" bgcolor=\"{Ground}\" style=\"background:{Ground};\">" +
            "<tr><td align=\"center\" style=\"padding:24px 16px;\">" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:560px;\">" +
            $"<tr><td style=\"padding:0 0 12px;font-family:{Mono};font-size:13px;line-height:20px;font-weight:700;letter-spacing:2px;color:{Primary};\">" +
            $"{brand}</td></tr>" +
            $"<tr><td bgcolor=\"{Card}\" style=\"background:{Card};border:1px solid {Hairline};border-top:3px solid {Primary};padding:24px;" +
            $"font-family:{Ui};font-size:16px;line-height:24px;color:{Ink};\">" +
            (eyebrow is null ? "" :
                $"<p style=\"margin:0 0 12px;font-family:{Mono};font-size:12px;line-height:18px;font-weight:700;letter-spacing:1px;text-transform:uppercase;" +
                $"color:{Accent};\">{Encode(eyebrow)}</p>") +
            string.Concat(blocks.Select(b => b.Html)) +
            (disclaimer is null ? "" :
                $"<p style=\"margin:16px 0 0;padding-top:16px;border-top:1px solid {Hairline};font-size:13px;line-height:20px;color:{InkSoft};\">{Encode(disclaimer)}</p>") +
            "</td></tr>" +
            $"<tr><td style=\"padding:16px 4px 0;font-family:{Ui};font-size:12px;line-height:18px;color:{InkSoft};\">{footer}</td></tr>" +
            "</table></td></tr></table></body></html>";
    }
}
