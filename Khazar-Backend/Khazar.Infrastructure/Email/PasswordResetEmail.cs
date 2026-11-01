using static Khazar.Infrastructure.Email.EmailLayout;

namespace Khazar.Infrastructure.Email;

/// <summary>Password reset link; the token travels only in this email, the database stores its hash.</summary>
internal static class PasswordResetEmail
{
    public const string Subject = "Khazar — şifrənin sıfırlanması";

    public static EmailMessage Create(
        string toEmail, string recipientName, string resetToken, TimeSpan validFor, StorefrontOptions storefront)
    {
        var minutes = (int)validFor.TotalMinutes;
        var resetUrl = storefront.Link("/sifre-yenile", ("email", toEmail), ("token", resetToken));

        var text = $"""
            Salam, {recipientName}!

            Khazar hesabınız ({toEmail}) üçün şifrə sıfırlama sorğusu aldıq. Yeni şifrə təyin etmək üçün bu linki açın:

            {resetUrl}

            Link {minutes} dəqiqə etibarlıdır və yalnız bir dəfə istifadə oluna bilər.

            TƏHLÜKƏSİZLİK: Bu sorğunu siz göndərməmisinizsə, məktubu nəzərə almayın — şifrəniz dəyişməyəcək.
            Khazar heç vaxt şifrənizi e-poçt və ya telefonla soruşmur.

            Khazar Textile
            """;

        var validityHtml = $"""
            <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center" style="margin:22px auto 0;">
              <tr><td bgcolor="{PanelBackground}" style="padding:9px 16px;background:{PanelBackground};border:1px solid {Border};border-radius:999px;font-family:{FontStack};font-size:12px;line-height:18px;color:{TextSecondary};">
                <span style="color:{Gold};">&#9201;</span>&nbsp; Link <strong style="color:{TextStrong};">{minutes} dəqiqə</strong> etibarlıdır · yalnız bir dəfə istifadə olunur
              </td></tr>
            </table>
            """;

        var html = Page(
            Subject,
            $"Yeni şifrə təyin etmək üçün link — {minutes} dəqiqə etibarlıdır.",
            Eyebrow("Hesab təhlükəsizliyi") +
            Heading("Şifrənizi sıfırlayın") +
            Paragraph($"Salam, {recipientName}! Khazar hesabınız ({toEmail}) üçün şifrə sıfırlama sorğusu aldıq. Yeni şifrə təyin etmək üçün aşağıdakı düyməyə klikləyin.") +
            Button(resetUrl, "Şifrəni sıfırla", large: true) +
            validityHtml +
            Notice(
                "⚠ Bu sorğunu siz göndərməmisiniz?",
                "Məktubu nəzərə almayın — şifrəniz dəyişməyəcək və hesabınız təhlükəsizdir. Khazar heç vaxt şifrənizi e-poçt və ya telefonla soruşmur; linki heç kimlə paylaşmayın.") +
            FallbackLink(resetUrl),
            "Bu məktub hesabınızın təhlükəsizliyi üçün avtomatik göndərildi.");

        return new EmailMessage(toEmail, recipientName, Subject, text, html, $"Reset link: {resetUrl} (valid for {minutes} minutes)");
    }
}
