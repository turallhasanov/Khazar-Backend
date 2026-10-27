using System.Globalization;
using System.Text;
using Khazar.Application.Common.Pricing;
using Khazar.Application.DTOs.Orders;
using static Khazar.Infrastructure.Email.EmailLayout;

namespace Khazar.Infrastructure.Email;

/// <summary>Order receipt: order details, line items, subtotal, shipping fee and grand total, shipping address.</summary>
internal static class OrderConfirmationEmail
{
    private static readonly CultureInfo Azerbaijani = CultureInfo.GetCultureInfo("az-Latn-AZ");

    // Baku has no daylight saving time; the fixed offset is the fallback where the IANA id is unavailable.
    private static readonly TimeZoneInfo BakuTime =
        TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Baku", out var zone)
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone("Asia/Baku", TimeSpan.FromHours(4), "Bakı", "Bakı");

    public static string Subject(string orderNumber) => $"Khazar — sifarişiniz qəbul olundu ({orderNumber})";

    public static EmailMessage Create(OrderResponseDto order, ShippingQuote shipping, StorefrontOptions storefront)
    {
        var placedAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc), BakuTime)
            .ToString("dd.MM.yyyy, HH:mm", Azerbaijani) + " (Bakı vaxtı)";
        var courier = shipping.IsExpress ? "Ekspress çatdırılma · eyni gün" : "Standart çatdırılma · 1–3 iş günü";
        var shippingFee = shipping.Fee == 0 ? "Pulsuz" : Money(shipping.Fee);
        var grandTotal = Money(order.TotalAmount + shipping.Fee);
        var catalogUrl = storefront.Link("/katalog");

        var textLines = new StringBuilder();
        foreach (var item in order.Items)
            textLines.AppendLine($"- {item.Name} ({item.Color} / {item.Size}) × {item.Quantity} — {Money(item.LineTotal)}");

        var text = $"""
            Təşəkkür edirik, {order.CustomerName}!

            Sifarişiniz qəbul olundu və hazırlanır.

            Sifariş nömrəsi: {order.OrderNumber}
            Tarix: {placedAt}

            {textLines.ToString().TrimEnd()}

            Ara cəm: {Money(order.TotalAmount)}
            Çatdırılma: {shippingFee}
            Ümumi cəm: {grandTotal}

            Çatdırılma ünvanı: {order.ShippingAddress}
            {courier}

            Sifarişiniz yola düşəndə sizə ayrıca məlumat veriləcək. Suallarınız üçün: hello@khazar.az

            Khazar Textile
            """;

        var html = Page(
            Subject(order.OrderNumber),
            $"Sifariş {order.OrderNumber} · {grandTotal} — qəbzi aşağıda tapın.",
            Eyebrow("Sifariş təsdiqi") +
            Heading($"Təşəkkür edirik, {order.CustomerName}!") +
            Paragraph("Sifarişiniz qəbul olundu və hazırlanır. Qəbziniz aşağıdadır.") +
            OrderMeta(order.OrderNumber, placedAt) +
            ItemsTable(order) +
            Totals(order.TotalAmount, shippingFee, grandTotal) +
            Panel(
                Label("Çatdırılma ünvanı") +
                $"""<p style="margin:0 0 4px;font-family:{FontStack};font-size:14px;line-height:22px;color:{TextStrong};">{Encode(order.CustomerName)}</p>""" +
                $"""<p style="margin:0 0 10px;font-family:{FontStack};font-size:14px;line-height:22px;color:{TextPrimary};">{Encode(order.ShippingAddress)}</p>""" +
                $"""<p style="margin:0;font-family:{FontStack};font-size:12px;line-height:18px;color:{Violet};">{Encode(courier)}</p>""",
                margin: "0 0 32px") +
            Button(catalogUrl, "Alış-verişə davam et") +
            Secondary("Sifarişiniz yola düşəndə sizə ayrıca məlumat veriləcək.", "20px 0 0") +
            Secondary("Suallarınız üçün: hello@khazar.az · +994 50 555 01 44", "4px 0 0"),
            $"Bu qəbz {Encode(order.CustomerEmail)} ünvanına {Encode(order.OrderNumber)} nömrəli sifariş üçün göndərildi.");

        return new EmailMessage(
            order.CustomerEmail, order.CustomerName, Subject(order.OrderNumber), text, html,
            $"Order {order.OrderNumber}: {order.ItemCount} items, subtotal {Money(order.TotalAmount)}, shipping {shippingFee}, total {grandTotal}");
    }

    // Two cells that stack on phones.
    private static string OrderMeta(string orderNumber, string placedAt) => Panel($"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
          <tr>
            <td class="kz-stack" width="50%" valign="top" style="font-family:{FontStack};">
              {Label("Sifariş nömrəsi")}
              <p style="margin:0;font-family:{MonoStack};font-size:17px;line-height:24px;font-weight:700;letter-spacing:1px;color:{TextStrong};">{Encode(orderNumber)}</p>
            </td>
            <td class="kz-stack kz-stack-gap" width="50%" valign="top" align="right" style="font-family:{FontStack};text-align:right;">
              {Label("Tarix")}
              <p style="margin:0;font-family:{FontStack};font-size:14px;line-height:24px;color:{TextPrimary};">{Encode(placedAt)}</p>
            </td>
          </tr>
        </table>
        """);

    private static string ItemsTable(OrderResponseDto order)
    {
        const string headerCell = "padding:0 0 12px;font-size:10px;line-height:14px;font-weight:600;letter-spacing:2px;";

        var rows = new StringBuilder();
        foreach (var item in order.Items)
        {
            rows.Append($"""
                <tr>
                  <td valign="top" style="padding:16px 12px 16px 0;border-top:1px solid {Border};font-family:{FontStack};">
                    <p style="margin:0;font-size:14px;line-height:20px;font-weight:600;color:{TextStrong};">{Encode(item.Name)}</p>
                    <p style="margin:3px 0 0;font-size:12px;line-height:18px;color:{TextSecondary};">{Encode(item.Color)} · {Encode(item.Size)} · {Encode(Money(item.UnitPrice))}</p>
                  </td>
                  <td valign="top" align="center" style="padding:16px 8px;border-top:1px solid {Border};font-family:{FontStack};font-size:14px;line-height:20px;color:{TextPrimary};white-space:nowrap;">× {item.Quantity}</td>
                  <td valign="top" align="right" style="padding:16px 0 16px 8px;border-top:1px solid {Border};font-family:{FontStack};font-size:14px;line-height:20px;font-weight:600;color:{TextStrong};white-space:nowrap;">{Encode(Money(item.LineTotal))}</td>
                </tr>
                """);
        }

        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:8px 0 0;">
              <tr>
                <td style="{headerCell}font-family:{FontStack};color:{TextMuted};">{Encode(Upper("Məhsul"))}</td>
                <td align="center" style="{headerCell}font-family:{FontStack};color:{TextMuted};">{Encode(Upper("Say"))}</td>
                <td align="right" style="{headerCell}font-family:{FontStack};color:{TextMuted};">{Encode(Upper("Məbləğ"))}</td>
              </tr>
              {rows}
            </table>
            """;
    }

    private static string Totals(decimal subtotal, string shippingFee, string grandTotal)
    {
        string Row(string label, string value, string valueColor) => $"""
            <tr>
              <td style="padding:5px 0;font-family:{FontStack};font-size:14px;line-height:20px;color:{TextSecondary};">{Encode(label)}</td>
              <td align="right" style="padding:5px 0;font-family:{FontStack};font-size:14px;line-height:20px;color:{valueColor};white-space:nowrap;">{Encode(value)}</td>
            </tr>
            """;

        return $"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:0 0 28px;border-top:1px solid {Border};">
              <tr><td colspan="2" style="padding-top:12px;font-size:0;line-height:0;">&nbsp;</td></tr>
              {Row("Ara cəm", Money(subtotal), TextPrimary)}
              {Row("Çatdırılma", shippingFee, shippingFee == "Pulsuz" ? Success : TextPrimary)}
              <tr><td colspan="2" style="padding:14px 0 0;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                  <tr><td bgcolor="{PanelBackground}" style="padding:16px 20px;background:{PanelBackground};border:1px solid {Violet};border-radius:10px;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                      <tr>
                        <td style="font-family:{FontStack};font-size:12px;line-height:20px;font-weight:600;letter-spacing:2px;color:{TextSecondary};">{Encode(Upper("Ümumi cəm"))}</td>
                        <td align="right" style="font-family:{FontStack};font-size:22px;line-height:28px;font-weight:700;color:{TextStrong};white-space:nowrap;">{Encode(grandTotal)}</td>
                      </tr>
                    </table>
                  </td></tr>
                </table>
              </td></tr>
            </table>
            """;
    }
}
