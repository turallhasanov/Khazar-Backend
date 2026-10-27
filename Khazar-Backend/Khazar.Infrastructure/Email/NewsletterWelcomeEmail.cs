using static Khazar.Infrastructure.Email.EmailLayout;

namespace Khazar.Infrastructure.Email;

/// <summary>Welcome email for new newsletter subscribers, with the optional welcome discount code in a promo box.</summary>
internal static class NewsletterWelcomeEmail
{
    public const string Subject = "Khazar — xəbərlərə xoş gəlmisiniz";

    private static readonly (string Title, string Text)[] Perks =
    [
        ("Yeni kolleksiyalar", "Mövsümün parçaları satışa çıxmazdan əvvəl."),
        ("Limited seriyalar", "Az sayda tikilən modellərə ilk çıxış."),
        ("Editoryal hekayələr", "Parça, ustalıq və stil haqqında jurnal yazıları."),
    ];

    public static EmailMessage Create(string toEmail, StorefrontOptions storefront)
    {
        var code = storefront.NewsletterDiscountCode;
        var percent = storefront.NewsletterDiscountPercent;
        var offer = percent > 0 ? $"İlk sifarişinizdə {percent}% endirim" : "İlk sifarişiniz üçün xüsusi endirim";
        var catalogUrl = storefront.Link("/katalog");
        var unsubscribeUrl = storefront.Link("/abunelikden-cix", ("email", toEmail));

        var text = $"""
            Khazar ailəsinə xoş gəlmisiniz!

            Xəbər bülleteninə abunə olduğunuz üçün təşəkkür edirik. Yeni kolleksiyalar, limited seriyalar və
            editoryal hekayələr barədə ilk siz xəbər tutacaqsınız.
            {(code.Length > 0 ? $"\n{offer} — promo kodunuz: {code}\n" : "")}
            Kolleksiyanı kəşf edin: {catalogUrl}

            Abunəlikdən çıxmaq üçün: {unsubscribeUrl}

            Khazar Textile
            """;

        var promoHtml = code.Length == 0 ? "" : $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:0 0 32px;">
              <tr><td align="center" bgcolor="{PanelBackground}" style="padding:26px 20px;background:{PanelBackground};border:2px dashed {Gold};border-radius:14px;font-family:{FontStack};">
                <p style="margin:0 0 10px;font-family:{FontStack};font-size:10px;line-height:14px;font-weight:600;letter-spacing:3px;color:{TextSecondary};">{Encode(Upper("Xoş gəldin hədiyyəsi"))}</p>
                <p class="kz-code" style="margin:0 0 10px;font-family:{MonoStack};font-size:30px;line-height:38px;font-weight:700;letter-spacing:6px;color:{Gold};">{Encode(code)}</p>
                <p style="margin:0;font-family:{FontStack};font-size:13px;line-height:20px;color:{TextPrimary};">{Encode(offer)}</p>
              </td></tr>
            </table>
            """;

        var perksHtml = string.Concat(Perks.Select(perk => $"""
            <tr>
              <td width="22" valign="top" style="padding:3px 0 14px;font-family:{FontStack};font-size:14px;line-height:20px;color:{Violet};">&#9670;</td>
              <td valign="top" style="padding:0 0 14px;font-family:{FontStack};">
                <p style="margin:0;font-size:14px;line-height:20px;font-weight:600;color:{TextStrong};">{Encode(perk.Title)}</p>
                <p style="margin:2px 0 0;font-size:13px;line-height:20px;color:{TextSecondary};">{Encode(perk.Text)}</p>
              </td>
            </tr>
            """));

        var html = Page(
            Subject,
            code.Length > 0 ? $"{offer} — promo kodunuz içəridə." : "Yeni kolleksiyalar barədə ilk siz xəbər tutacaqsınız.",
            Eyebrow("Newsletter") +
            Heading("Khazar ailəsinə xoş gəlmisiniz") +
            Paragraph("Xəbər bülleteninə abunə olduğunuz üçün təşəkkür edirik. Avropa parçalarından tikilən yeni kolleksiyalar barədə ilk siz xəbər tutacaqsınız.") +
            promoHtml +
            $"""<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:0 0 20px;">{perksHtml}</table>""" +
            Button(catalogUrl, "Kolleksiyanı kəşf et"),
            $"""
            Bu məktubu {Encode(toEmail)} ünvanı Khazar xəbərlərinə abunə olduğu üçün aldınız.<br>
            <a href="{Encode(unsubscribeUrl)}" target="_blank" style="color:{TextSecondary};text-decoration:underline;">Abunəlikdən çıx</a>
            """);

        var logDetail = code.Length > 0 ? $"Discount code: {code}" : "No discount code configured.";
        return new EmailMessage(toEmail, string.Empty, Subject, text, html, logDetail);
    }
}
