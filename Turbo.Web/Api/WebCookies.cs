using System;
using Microsoft.AspNetCore.Http;
using Turbo.Web.Configuration;

namespace Turbo.Web.Api;

/// <summary>
/// The site's cookies: the sign-in, a sign-up waiting for its name, and the OAuth state. All
/// HttpOnly so no script reads them, SameSite=Lax so another site can't post with them, and
/// secure when the site is https.
/// </summary>
internal static class WebCookies
{
    public const string SESSION = "turbo_session";
    public const string SIGN_UP = "turbo_signup";
    public const string STATE = "turbo_oauth_state";

    public static void Set(
        HttpContext http,
        WebConfig config,
        string name,
        string value,
        DateTimeOffset expires
    ) =>
        http.Response.Cookies.Append(
            name,
            value,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = config.SiteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                Path = "/",
                Expires = expires,
                IsEssential = true,
            }
        );

    public static void Clear(HttpContext http, string name) =>
        http.Response.Cookies.Delete(name, new CookieOptions { Path = "/" });

    public static string? Get(HttpContext http, string name) =>
        http.Request.Cookies.TryGetValue(name, out var value) ? value : null;
}
