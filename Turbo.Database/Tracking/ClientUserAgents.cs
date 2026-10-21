using System;

namespace Turbo.Database.Tracking;

/// <summary>
/// The browser, system and kind of device a user agent names, as the few families the Client
/// health page groups by. Only these are kept for grouping; the user agent itself stays on the raw
/// report until it expires.
/// </summary>
public static class ClientUserAgents
{
    public const string UNKNOWN = "unknown";

    public static (string Browser, string Os, string Device) Classify(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent) || userAgent == UNKNOWN)
            return (UNKNOWN, UNKNOWN, UNKNOWN);

        return (BrowserOf(userAgent), OsOf(userAgent), DeviceOf(userAgent));
    }

    // Order matters: Edge, Opera and Samsung Internet also say Chrome, and Chrome also says Safari.
    private static string BrowserOf(string ua) =>
        Has(ua, "Edg/") || Has(ua, "EdgA/") || Has(ua, "EdgiOS/") ? "Edge"
        : Has(ua, "OPR/") || Has(ua, "Opera") ? "Opera"
        : Has(ua, "SamsungBrowser/") ? "Samsung Internet"
        : Has(ua, "Firefox/") || Has(ua, "FxiOS/") ? "Firefox"
        : Has(ua, "Electron/") ? "Electron"
        : Has(ua, "Chrome/") || Has(ua, "CriOS/") || Has(ua, "Chromium/") ? "Chrome"
        : Has(ua, "Safari/") ? "Safari"
        : "Other";

    // iPadOS asks for desktop pages and says Macintosh; the device check below tells it apart.
    private static string OsOf(string ua) =>
        Has(ua, "Windows") ? "Windows"
        : Has(ua, "Android") ? "Android"
        : Has(ua, "iPhone") || Has(ua, "iPad") || Has(ua, "iPod") ? "iOS"
        : Has(ua, "CrOS") ? "ChromeOS"
        : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
        : Has(ua, "Linux") ? "Linux"
        : "Other";

    private static string DeviceOf(string ua) =>
        Has(ua, "iPad") || Has(ua, "Tablet") || (Has(ua, "Android") && !Has(ua, "Mobile"))
            ? "tablet"
        : Has(ua, "Mobi") || Has(ua, "iPhone") || Has(ua, "iPod") ? "mobile"
        : "desktop";

    private static bool Has(string ua, string part) =>
        ua.Contains(part, StringComparison.OrdinalIgnoreCase);
}
