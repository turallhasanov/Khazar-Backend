using System.Net;

namespace Khazar.Infrastructure.Email;

/// <summary>Welcome email with the confirmation code, in Azerbaijani like the storefront.</summary>
internal static class VerificationEmail
{
    public const string Subject = "Khazar — e-poçt ünvanınızı təsdiqləyin";

    public static string TextBody(string recipientName, string code, TimeSpan validFor) => $"""
        Salam, {recipientName}!

        Khazar-a xoş gəlmisiniz. Hesabınızı aktivləşdirmək üçün təsdiq kodunuz:

        {code}

        Kod {(int)validFor.TotalMinutes} dəqiqə etibarlıdır. Qeydiyyatdan siz keçməmisinizsə, bu məktubu nəzərə almayın.

        Khazar Textile
        """;

    public static string HtmlBody(string recipientName, string code, TimeSpan validFor)
    {
        var name = WebUtility.HtmlEncode(recipientName);
        return $"""
            <!doctype html>
            <html lang="az">
            <body style="margin:0;padding:32px 16px;background:#f7f4ee;font-family:Georgia,'Times New Roman',serif;color:#1d1c1a;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:480px;margin:0 auto;background:#fffdf9;border:1px solid #e6e0d4;">
                <tr><td style="padding:40px 32px;text-align:center;">
                  <p style="margin:0;font-size:22px;letter-spacing:2px;">Khazar</p>
                  <p style="margin:4px 0 32px;font-size:9px;letter-spacing:5px;text-transform:uppercase;color:#8a877f;">Textile</p>
                  <p style="margin:0 0 8px;font-size:18px;">Salam, {name}!</p>
                  <p style="margin:0 0 28px;font-family:Arial,sans-serif;font-size:14px;line-height:1.6;color:#55534d;">
                    Khazar-a xoş gəlmisiniz. Hesabınızı aktivləşdirmək üçün aşağıdakı kodu daxil edin.
                  </p>
                  <p style="margin:0 0 28px;font-family:'Courier New',monospace;font-size:32px;letter-spacing:10px;font-weight:bold;">{code}</p>
                  <p style="margin:0;font-family:Arial,sans-serif;font-size:12px;line-height:1.6;color:#8a877f;">
                    Kod {(int)validFor.TotalMinutes} dəqiqə etibarlıdır. Qeydiyyatdan siz keçməmisinizsə, bu məktubu nəzərə almayın.
                  </p>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
