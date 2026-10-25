using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Turbo.Contracts.Plugins.Web;

/// <summary>
/// A call to a plugin's web route (<see cref="IPluginWebRoute"/>): its route values (from the
/// pattern), its query, its JSON body (null without one), the player making it (null on an
/// anonymous route when nobody is signed in) and the address it came from.
/// </summary>
public sealed record PluginWebRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Route,
    IReadOnlyDictionary<string, string> Query,
    JsonElement? Body,
    int? PlayerId,
    string RemoteAddress
)
{
    /// <summary>
    /// The request's headers by lower-case name (looked up in any case), a header sent more than
    /// once joined with <c>", "</c>. The caller's credentials (<c>authorization</c>, <c>cookie</c>,
    /// <c>proxy-authorization</c>) are left out unless the route asks for them
    /// (<see cref="IPluginWebRoute.CredentialHeaders"/>): the host has already signed the caller in.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// The body exactly as it was sent, empty without one. What a webhook's signature is worked
    /// out over: <see cref="Body"/> is the same bytes parsed, and parsing does not keep them.
    /// </summary>
    public ReadOnlyMemory<byte> RawBody { get; init; }
}
