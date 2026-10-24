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
);
