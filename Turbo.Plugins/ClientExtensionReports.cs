using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// What players' clients report about the plugins' client halves (<see cref="IClientExtensionReports"/>),
/// in memory: each distinct report per plugin, the players who sent it and when one last did. Bounded
/// against a client sending anything: so many plugins, reports per plugin and players per report,
/// each text cut to length. A module a client refused or that threw, a patch that names an
/// element its template lacks and one that changes nothing the window draws, is logged here too, naming what to change: the client's console is
/// not where a hotel owner looks. Logged once per report and quarter hour, however many clients send it.
/// </summary>
internal sealed partial class ClientExtensionReports(
    ILogger<ClientExtensionReports> logger,
    TimeProvider time
) : IClientExtensionReports
{
    public const int MAX_OWNERS = 100;
    public const int MAX_REPORTS_PER_OWNER = 50;
    public const int MAX_PLAYERS_PER_REPORT = 1000;
    public const int MAX_TEXT = 300;

    /// <summary>
    /// A message names the template, plugin, element and value and then says what to change, and a
    /// bind the client skips says so for each field; cut at <see cref="MAX_TEXT"/> it lost the fix.
    /// </summary>
    public const int MAX_MESSAGE = 1000;
    public static readonly TimeSpan LOG_EVERY = TimeSpan.FromMinutes(15);

    /// <summary>The external variable the client checks a module's origin against.</summary>
    public const string HOSTS_SETTING = "client.modules.hosts";

    private readonly Dictionary<string, Dictionary<Key, Entry>> _owners = new(
        StringComparer.OrdinalIgnoreCase
    );

    public void Report(long playerId, ImmutableArray<ClientExtensionReport> reports)
    {
        if (playerId <= 0 || reports.IsDefaultOrEmpty)
            return;

        var now = time.GetUtcNow().UtcDateTime;

        foreach (var report in reports)
        {
            if (
                !ClientExtensionReportKinds.ALL.Contains(report.Kind)
                || !OwnerKey().IsMatch(report.Owner)
            )
                continue;

            var key = new Key(report.Kind, Cut(report.Subject), Cut(report.Reason));
            Entry entry;
            bool log;

            lock (_owners)
            {
                if (!_owners.TryGetValue(report.Owner, out var entries))
                {
                    if (_owners.Count >= MAX_OWNERS)
                        continue;

                    _owners[report.Owner] = entries = [];
                }

                if (!entries.TryGetValue(key, out entry!))
                {
                    if (entries.Count >= MAX_REPORTS_PER_OWNER)
                        continue;

                    entries[key] = entry = new Entry();
                }

                if (entry.Players.Count < MAX_PLAYERS_PER_REPORT)
                    entry.Players.Add(playerId);

                entry.Message = Cut(report.Message, MAX_MESSAGE);
                entry.LastUtc = now;
                log = entry.LoggedUtc is not { } logged || now - logged >= LOG_EVERY;

                if (log)
                    entry.LoggedUtc = now;
            }

            if (log)
                Log(report.Owner, key, entry.Message);
        }
    }

    public ImmutableArray<PluginClientIssue> IssuesOf(string owner)
    {
        lock (_owners)
            return _owners.TryGetValue(owner, out var entries)
                ?
                [
                    .. entries
                        .OrderBy(x => x.Key.Kind, StringComparer.Ordinal)
                        .ThenBy(x => x.Key.Subject, StringComparer.Ordinal)
                        .Select(x => new PluginClientIssue(
                            x.Key.Kind,
                            x.Key.Subject,
                            x.Value.Message,
                            x.Value.Players.Count,
                            x.Value.LastUtc
                        )),
                ]
                : [];
    }

    private void Log(string owner, Key key, string message)
    {
        switch (key.Kind)
        {
            case ClientExtensionReportKinds.MODULE_REFUSED
                when key.Reason == ClientExtensionReportKinds.HOST_NOT_ALLOWED:
                logger.LogWarning(
                    "A client refused plugin {Owner}'s client module {Module}: its origin is not in the external variable {Setting}. Add the origin to that variable (admin panel: Texts & variables), or serve plugin files from the client's own origin. Client said: {Message}",
                    owner,
                    key.Subject,
                    HOSTS_SETTING,
                    message
                );
                break;
            case ClientExtensionReportKinds.MODULE_REFUSED:
            case ClientExtensionReportKinds.MODULE_FAILED:
                logger.LogWarning(
                    "A client did not run plugin {Owner}'s client module {Module} ({Reason}). Client said: {Message}",
                    owner,
                    key.Subject,
                    key.Reason,
                    message
                );
                break;
            case ClientExtensionReportKinds.PATCH_UNMATCHED:
                logger.LogWarning(
                    "Plugin {Owner}'s template patch names an element {Template} does not have: fix the element key in its wwwroot/template-patches.json or api.templates.patch call (alt-click the element in the client's dev inspector for its key). Client said: {Message}",
                    owner,
                    key.Subject,
                    message
                );
                break;
            case ClientExtensionReportKinds.PATCH_INEFFECTIVE:
                logger.LogWarning(
                    // A field the window's code sets too (`<element>.<field>`), a bind the client cannot bind
                    // (`<element>:bind`) or an asset that did not load (`<element>:<url>`): the client says which.
                    "Plugin {Owner}'s template patch of {Template} changes nothing at {Field}. Client said: {Message}",
                    owner,
                    key.Subject,
                    key.Reason,
                    message
                );
                break;
            case ClientExtensionReportKinds.PATCH_CONFLICT:
                logger.LogInformation(
                    "Plugin {Owner}'s template patch of {Template} sets what another plugin's sets too; the later order wins. Client said: {Message}",
                    owner,
                    key.Subject,
                    message
                );
                break;
        }
    }

    private static string Cut(string? text, int max = MAX_TEXT) =>
        text is null ? string.Empty
        : text.Length > max ? text[..max]
        : text;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex OwnerKey();

    private readonly record struct Key(string Kind, string Subject, string Reason);

    private sealed class Entry
    {
        public HashSet<long> Players { get; } = [];
        public string Message { get; set; } = string.Empty;
        public DateTime LastUtc { get; set; }
        public DateTime? LoggedUtc { get; set; }
    }
}
