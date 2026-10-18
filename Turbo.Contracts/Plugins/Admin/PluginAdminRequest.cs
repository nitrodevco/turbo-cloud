using System.Collections.Generic;
using System.Text.Json;

namespace Turbo.Contracts.Plugins.Admin;

/// <summary>
/// A call to a plugin's admin endpoint: its route values (from the pattern), its query, its JSON
/// body (null without one) and the staff member making it.
/// </summary>
public sealed record PluginAdminRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Route,
    IReadOnlyDictionary<string, string> Query,
    JsonElement? Body,
    int PlayerId
);
