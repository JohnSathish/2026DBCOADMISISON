namespace ERP.Infrastructure.Payments;

/// <summary>
/// Razorpay API keys. In the Razorpay Dashboard (Settings → Payment Methods), enable UPI,
/// UPI Intent, and UPI QR for the account linked to <see cref="KeyId"/> so web checkout can offer
/// QR on desktop and app intent on mobile.
/// </summary>
public class RazorpaySettings
{
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
    public bool TestMode { get; set; } = true;
    public decimal ApplicationFeeAmount { get; set; } = 600.00m;

    /// <summary>
    /// Full URL to a square-ish logo (PNG/JPG/SVG) passed to Standard Checkout as <c>image</c>.
    /// Must be publicly reachable (HTTPS in production). If empty, the client omits <c>image</c> so
    /// Razorpay uses the logo from Dashboard → Settings → Branding.
    /// </summary>
    public string? CheckoutLogoUrl { get; set; }
}


















