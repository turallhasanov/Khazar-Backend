using System.Globalization;
using System.Net;

namespace Khazar.Infrastructure.Email;

/// <summary>
/// Dark "glass" design system of the Khazar emails (newsletter, order receipt, password reset).
/// Built for mail clients rather than browsers: table layout, inline styles on every element, bgcolor attributes and
/// an MSO fixed-width table for Outlook, a solid color under every gradient (Outlook and some webmail drop CSS
/// gradients), and a single media query for phones (ignored where unsupported; the layout is fluid anyway).
/// </summary>
internal static class EmailLayout
{
    // Palette
    public const string PageBackground = "#090D16";
    public const string CardBackground = "#0F172A";
    public const string PanelBackground = "#1E293B";
    public const string Border = "#334155";
    public const string TextPrimary = "#E2E8F0";
    public const string TextStrong = "#F8FAFC";
    public const string TextSecondary = "#94A3B8";
    public const string TextMuted = "#64748B";
    public const string Indigo = "#6366F1";
    public const string Violet = "#8B5CF6";
    public const string Gold = "#F59E0B";
    public const string Success = "#34D399";

    public const string FontStack = "Inter,'Segoe UI',Roboto,'Helvetica Neue',Helvetica,Arial,sans-serif";
    public const string MonoStack = "'SFMono-Regular',Consolas,'Liberation Mono',Menlo,monospace";

    private const string AccentGradient = $"linear-gradient(90deg,{Indigo} 0%,{Violet} 55%,{Gold} 100%)";
    private const string ButtonGradient = $"linear-gradient(135deg,{Indigo} 0%,{Violet} 100%)";

    private static readonly CultureInfo Azerbaijani = CultureInfo.GetCultureInfo("az-Latn-AZ");

    public static string Encode(string value) => WebUtility.HtmlEncode(value);

    /// <summary>
    /// Upper-cases with Azerbaijani rules (ə → Ə, i → İ). Labels are upper-cased here rather than with CSS
    /// text-transform, which some renderers apply without the language rules and leave "ə" lower-case.
    /// </summary>
    public static string Upper(string text) => text.ToUpper(Azerbaijani);

    /// <summary>Amount in the storefront's format, e.g. "1 234,50 ₼" (the API's only currency is AZN).</summary>
    public static string Money(decimal amount) => string.Format(Azerbaijani, "{0:N2} ₼", amount);

    /// <param name="title">Document title; some clients show it in the window or tab.</param>
    /// <param name="preheader">Preview line shown next to the subject in the inbox list; hidden in the email.</param>
    /// <param name="contentHtml">Card body built from the helpers below; callers HTML-encode every value they insert.</param>
    /// <param name="footerHtml">Small print under the card, e.g. the reason for the email and an unsubscribe link.</param>
    public static string Page(string title, string preheader, string contentHtml, string footerHtml = "") => $$"""
        <!doctype html>
        <html lang="az" xmlns="http://www.w3.org/1999/xhtml" xmlns:o="urn:schemas-microsoft-com:office:office">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta http-equiv="X-UA-Compatible" content="IE=edge">
          <meta name="color-scheme" content="dark">
          <meta name="supported-color-schemes" content="dark">
          <meta name="x-apple-disable-message-reformatting">
          <title>{{Encode(title)}}</title>
          <!--[if mso]><style>table,td,p,a,span{font-family:Arial,sans-serif !important;}</style><![endif]-->
          <!--[if !mso]><!--><link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet"><!--<![endif]-->
          <style>
            :root { color-scheme: dark; supported-color-schemes: dark; }
            body { margin:0 !important; padding:0 !important; width:100% !important; background:{{PageBackground}}; }
            a { color:{{Violet}}; }
            @media only screen and (max-width:620px) {
              .kz-container { width:100% !important; }
              .kz-px { padding-left:22px !important; padding-right:22px !important; }
              .kz-stack { display:block !important; width:100% !important; text-align:left !important; }
              .kz-stack-gap { padding-top:14px !important; }
              .kz-btn { width:100% !important; }
              .kz-btn a { display:block !important; }
              .kz-code { font-size:22px !important; letter-spacing:3px !important; }
              .kz-h1 { font-size:24px !important; line-height:32px !important; }
            }
          </style>
        </head>
        <body style="margin:0;padding:0;background:{{PageBackground}};-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;">
          <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;mso-hide:all;font-size:1px;line-height:1px;color:{{PageBackground}};">{{Encode(preheader)}}&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;&#8203;&nbsp;</div>
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" bgcolor="{{PageBackground}}" style="background:{{PageBackground}};">
            <tr><td align="center" style="padding:40px 12px;">
              <!--[if mso]><table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0"><tr><td><![endif]-->
              <table role="presentation" class="kz-container" width="600" cellspacing="0" cellpadding="0" border="0" style="width:600px;max-width:600px;">
                <tr><td align="center" style="padding:0 0 28px;">{{Logo()}}</td></tr>
                <tr><td bgcolor="{{CardBackground}}" style="background:{{CardBackground}};border:1px solid {{Border}};border-radius:16px;overflow:hidden;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                    <tr><td height="4" bgcolor="{{Violet}}" style="height:4px;line-height:4px;font-size:0;background-color:{{Violet}};background-image:{{AccentGradient}};border-radius:16px 16px 0 0;">&nbsp;</td></tr>
                    <tr><td class="kz-px" style="padding:44px 48px 48px;font-family:{{FontStack}};color:{{TextPrimary}};">
                      {{contentHtml}}
                    </td></tr>
                  </table>
                </td></tr>
                <tr><td class="kz-px" align="center" style="padding:28px 32px 0;font-family:{{FontStack}};font-size:12px;line-height:20px;color:{{TextMuted}};">
                  {{footerHtml}}
                  <p style="margin:14px 0 0;font-family:{{FontStack}};font-size:11px;line-height:18px;color:{{TextMuted}};">
                    Khazar Textile · Ülviyyə Bünyadova 74, Bakı, Azərbaycan · <a href="mailto:hello@khazar.az" style="color:{{TextSecondary}};text-decoration:none;">hello@khazar.az</a>
                  </p>
                </td></tr>
              </table>
              <!--[if mso]></td></tr></table><![endif]-->
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string Logo() => $"""
        <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center">
          <tr><td align="center" style="font-family:{FontStack};font-size:22px;line-height:26px;font-weight:700;letter-spacing:9px;color:{TextStrong};padding-left:9px;">KHAZAR</td></tr>
          <tr><td align="center" style="padding-top:6px;font-family:{FontStack};font-size:9px;line-height:12px;font-weight:600;letter-spacing:6px;color:{Gold};padding-left:6px;">TEXTILE</td></tr>
        </table>
        """;

    /// <summary>Small uppercase label above the heading, e.g. "SİFARİŞ TƏSDİQİ".</summary>
    public static string Eyebrow(string text, string color = Violet) =>
        $"""<p style="margin:0 0 12px;font-family:{FontStack};font-size:11px;line-height:16px;font-weight:600;letter-spacing:3px;color:{color};">{Encode(Upper(text))}</p>""";

    public static string Heading(string text) =>
        $"""<h1 class="kz-h1" style="margin:0 0 16px;font-family:{FontStack};font-size:28px;line-height:36px;font-weight:700;letter-spacing:-0.3px;color:{TextStrong};">{Encode(text)}</h1>""";

    public static string Paragraph(string text) =>
        $"""<p style="margin:0 0 24px;font-family:{FontStack};font-size:15px;line-height:24px;color:{TextPrimary};">{Encode(text)}</p>""";

    public static string Secondary(string text, string margin = "0 0 24px") =>
        $"""<p style="margin:{margin};font-family:{FontStack};font-size:13px;line-height:21px;color:{TextSecondary};">{Encode(text)}</p>""";

    /// <summary>
    /// Bulletproof CTA: the link fills a table cell with a solid background, so the button stays clickable and colored
    /// in Outlook (no gradient or rounded corners there); <paramref name="large"/> for the primary action of the email.
    /// </summary>
    public static string Button(string url, string label, bool large = false)
    {
        var padding = large ? "18px 44px" : "15px 34px";
        var size = large ? "16px" : "14px";
        return $"""
            <table role="presentation" class="kz-btn" cellspacing="0" cellpadding="0" border="0" align="center" style="margin:8px auto 8px;">
              <tr><td align="center" bgcolor="{Indigo}" style="border-radius:8px;background-color:{Indigo};background-image:{ButtonGradient};box-shadow:0 8px 24px rgba(99,102,241,0.35);">
                <a href="{Encode(url)}" target="_blank" style="display:inline-block;padding:{padding};font-family:{FontStack};font-size:{size};line-height:20px;font-weight:600;letter-spacing:0.3px;color:#FFFFFF;text-decoration:none;border-radius:8px;">{Encode(label)}</a>
              </td></tr>
            </table>
            """;
    }

    /// <summary>The raw URL under a button, for clients that block buttons or strip links.</summary>
    public static string FallbackLink(string url) => $"""
        <p style="margin:20px 0 0;font-family:{FontStack};font-size:12px;line-height:19px;color:{TextMuted};text-align:center;">
          Düymə işləmirsə, bu linki brauzerinizə köçürün:<br>
          <a href="{Encode(url)}" target="_blank" style="color:{Violet};word-break:break-all;text-decoration:underline;">{Encode(url)}</a>
        </p>
        """;

    /// <summary>A raised inner panel (#1E293B) for grouped content such as the receipt or the address.</summary>
    public static string Panel(string innerHtml, string padding = "22px 24px", string margin = "0 0 20px") => $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:{margin};">
          <tr><td bgcolor="{PanelBackground}" style="padding:{padding};background:{PanelBackground};border:1px solid {Border};border-radius:12px;font-family:{FontStack};">
            {innerHtml}
          </td></tr>
        </table>
        """;

    /// <summary>Uppercase caption inside a panel.</summary>
    public static string Label(string text) =>
        $"""<p style="margin:0 0 6px;font-family:{FontStack};font-size:10px;line-height:14px;font-weight:600;letter-spacing:2px;color:{TextMuted};">{Encode(Upper(text))}</p>""";

    /// <summary>Highlighted notice with a colored left edge, e.g. the security warning of the reset email.</summary>
    public static string Notice(string title, string text, string accent = Gold) => $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:28px 0 0;">
          <tr><td bgcolor="{PanelBackground}" style="padding:18px 20px;background:{PanelBackground};border:1px solid {Border};border-left:4px solid {accent};border-radius:10px;font-family:{FontStack};">
            <p style="margin:0 0 6px;font-family:{FontStack};font-size:14px;line-height:20px;font-weight:600;color:{accent};">{Encode(title)}</p>
            <p style="margin:0;font-family:{FontStack};font-size:13px;line-height:21px;color:{TextSecondary};">{Encode(text)}</p>
          </td></tr>
        </table>
        """;

    public static string Divider(string margin = "28px 0") => $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:{margin};">
          <tr><td height="1" bgcolor="{Border}" style="height:1px;line-height:1px;font-size:0;background:{Border};">&nbsp;</td></tr>
        </table>
        """;
}
