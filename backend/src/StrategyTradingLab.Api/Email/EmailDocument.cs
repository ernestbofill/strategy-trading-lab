using System.Net;
using System.Text;

namespace StrategyTradingLab.Api.Email;

/// <summary>An email ready to send: the plain text and the same content as HTML (spec: Emails).</summary>
public record EmailContent(string Subject, string Text, string Html);

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

    bool Ca => locale == "ca";

    static string Encode(string text) => WebUtility.HtmlEncode(text);

    /// <summary>Encoded, with line breaks kept.</summary>
    static string Lines(string text) => Encode(text).Replace("\n", "<br>");

    EmailDocument Add(string text, string html)
    {
        blocks.Add((text, html));
        return this;
    }

    /// <summary>The small uppercase label above the content (HTML only), e.g. "Signal · Live".</summary>
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

    /// <summary>A quote of what the user selected, e.g. About: "ADD A STOCK".</summary>
    public EmailDocument Quote(string label, string text) =>
        Add($"{label}: \"{text}\"",
            $"<p style=\"margin:0 0 4px;font-size:13px;color:{InkSoft};\">{Encode(label)}</p>" +
            $"<p style=\"margin:0 0 16px;padding:8px 12px;border-left:3px solid {Hairline};font-style:italic;\">{Lines(text)}</p>");

    /// <summary>The main link: the lead and the address in the text, a button with the link under it in the HTML.</summary>
    public EmailDocument Action(string lead, string button, string url) =>
        Add($"{lead}\n{url}",
            $"<p style=\"margin:0 0 12px;\">{Encode(lead.TrimEnd(':'))}.</p>" +
            $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 8px;\"><tr>" +
            $"<td bgcolor=\"{Primary}\" style=\"background:{Primary};border-radius:2px;\">" +
            $"<a href=\"{Encode(url)}\" style=\"display:inline-block;padding:12px 20px;font-family:{Mono};font-size:14px;line-height:20px;font-weight:700;" +
            $"letter-spacing:0.5px;text-transform:uppercase;color:{OnAction};text-decoration:none;\">{Encode(button)}</a></td></tr></table>" +
            $"<p style=\"margin:0 0 20px;font-size:12px;line-height:18px;color:{InkSoft};word-break:break-all;\">" +
            $"{Encode(Ca ? "O obre aquest enllaç:" : "Or open this link:")} <a href=\"{Encode(url)}\" style=\"color:{InkSoft};\">{Encode(url)}</a></p>");

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
    public EmailDocument Section(string title, IEnumerable<EmailItem> items)
    {
        var list = items.ToList();
        var text = new StringBuilder(title);
        foreach (var item in list)
            text.Append("\n- ").Append(item.Tag is null ? "" : $"({item.Tag}) ").Append(item.Text).Append(item.Url is null ? "" : $" {item.Url}");

        var html = new StringBuilder(
            $"<h2 style=\"margin:24px 0 4px;padding-bottom:6px;border-bottom:1px solid {Hairline};font-family:{Mono};font-size:13px;line-height:20px;" +
            $"font-weight:700;letter-spacing:1px;text-transform:uppercase;color:{Accent};\">{Encode(title)}</h2>" +
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 8px;\">");
        foreach (var item in list)
        {
            var color = item.Live ? HotPink : Info;
            var tag = item.Tag is null ? "" :
                $"<span style=\"display:inline-block;margin-right:6px;padding:0 6px;border:1px solid {color};border-radius:11px;font-family:{Mono};" +
                $"font-size:11px;line-height:18px;letter-spacing:0.5px;text-transform:uppercase;color:{color};\">{Encode(item.Tag)}</span>";
            var body = item.Url is null ? Encode(item.Text)
                : $"<a href=\"{Encode(item.Url)}\" style=\"color:{Ink};text-decoration:underline;text-decoration-color:{Primary};\">{Encode(item.Text)}</a>" +
                  $" <a href=\"{Encode(item.Url)}\" style=\"color:{Primary};text-decoration:none;\">&rarr;</a>";
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

    public EmailContent Build(string subject)
    {
        var text = string.Join("\n\n", blocks.Select(b => b.Text).Append(disclaimer).OfType<string>());
        return new(subject, text, Html(subject));
    }

    string Html(string subject)
    {
        var lang = Ca ? "ca" : "en";
        var line = preheader?.Replace('\n', ' ') ?? "";
        var preview = Encode(line.Length > 140 ? line[..139] + "…" : line);
        var brand = appUrl is null ? "STRATEGY TRADING LAB"
            : $"<a href=\"{Encode(appUrl)}/\" style=\"color:{Primary};text-decoration:none;\">STRATEGY TRADING LAB</a>";
        // No "you get this because you have an account": a sign-up confirmation can reach someone who never signed up.
        var footer = "Strategy Trading Lab" + (appUrl is null ? ""
            : $" &middot; <a href=\"{Encode(appUrl)}/\" style=\"color:{InkSoft};\">{Encode(Ca ? "Obre l'aplicació" : "Open the app")}</a>");

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
            $"<span style=\"color:{HotPink};\">&#9650;</span> {brand}</td></tr>" +
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
