using System;
using System.Collections.Generic;

namespace Turbo.Contracts.Plugins.Web;

/// <summary>
/// A plugin web route's answer: a status with a JSON value, an error message
/// (<c>{"error": "..."}</c>) the caller shows, a page (<see cref="Html"/>) or a redirect
/// (<see cref="Redirect"/>), with the few headers a route may set (<see cref="Headers"/>).
/// </summary>
public sealed record PluginWebResult(int Status, object? Value = null, string? Error = null)
{
    /// <summary>
    /// The headers a route may answer: <c>WWW-Authenticate</c> (where to sign in, on a 401),
    /// <c>Cache-Control</c> and <c>Pragma</c> (a token's answer is never cached), <c>Retry-After</c>
    /// and <c>Location</c> (set by <see cref="Redirect"/>). The host sets the rest itself.
    /// </summary>
    public static readonly IReadOnlySet<string> HEADERS = new HashSet<string>(
        ["WWW-Authenticate", "Cache-Control", "Pragma", "Retry-After", "Location"],
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>Headers of <see cref="HEADERS"/> to answer with; any other is refused (a 500, logged).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// A page of the plugin's, sent as <c>text/html</c> instead of <see cref="Value"/>. The host
    /// sends it with a content security policy that runs no script and loads nothing from
    /// elsewhere (inline styles and images from the hotel and <c>data:</c> only) and that no other
    /// site may frame it: a form on it is what it can do.
    /// </summary>
    public string? Page { get; init; }

    public static PluginWebResult Ok(object? value) => new(200, value);

    public static PluginWebResult NoContent() => new(204);

    /// <summary>Taken, with nothing to answer: 202 with no body (an MCP notification's answer).</summary>
    public static PluginWebResult Accepted() => new(202);

    public static PluginWebResult BadRequest(string error) => new(400, Error: error);

    public static PluginWebResult Forbidden(string error) => new(403, Error: error);

    public static PluginWebResult NotFound(string error) => new(404, Error: error);

    /// <summary>A page (<see cref="Page"/>), 200 unless <paramref name="status"/> says otherwise.</summary>
    public static PluginWebResult Html(string page, int status = 200) =>
        new(status) { Page = page };

    /// <summary>
    /// 302 to <paramref name="url"/> (an absolute <c>http</c> or <c>https</c> address, or one on
    /// the hotel starting with <c>/</c>). Redirect only to an address the route checked, such as
    /// an OAuth client's registered one: never to one a caller made up.
    /// </summary>
    public static PluginWebResult Redirect(string url) =>
        new(302) { Headers = new Dictionary<string, string> { ["Location"] = url } };
}
