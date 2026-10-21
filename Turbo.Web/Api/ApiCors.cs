using System;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Turbo.Web.Api;

/// <summary>Which sites' pages may read the public API's answers from a browser (CORS).</summary>
internal static class ApiCors
{
    /// <summary>
    /// Lets a page of an allowed site read the answer (<c>Access-Control-Allow-Origin</c>: <c>*</c>
    /// when any site is, else the site itself), and its <c>Retry-After</c>; never with
    /// credentials. Whether the request's site is allowed.
    /// </summary>
    public static bool AllowOrigin(HttpContext http, string[] allowedOrigins)
    {
        var origin = http.Request.Headers.Origin.ToString();

        if (origin.Length == 0)
            return false;

        var headers = http.Response.Headers;

        if (allowedOrigins.Contains("*"))
            headers.AccessControlAllowOrigin = "*";
        else if (
            allowedOrigins.Any(allowed =>
                string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            headers.AccessControlAllowOrigin = origin;
            headers.Vary = "Origin";
        }
        else
            return false;

        headers.AccessControlExposeHeaders = "Retry-After";

        return true;
    }
}
