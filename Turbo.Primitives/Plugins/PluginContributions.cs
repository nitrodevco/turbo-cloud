using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// What one load of a plugin registers, noted by whatever registers it: the host's assembly scan
/// (commands, furni logic, listeners, handlers, messages...), the plugin manager (settings, web
/// routes, admin pages, databases, client module, template patches) and any registry a plugin
/// adds for others. The plugin's own container holds one per load, so a registry finds it with
/// <see cref="PluginContributionLogExtensions.Contributes"/> on the services it registers with;
/// core's own registrations, made with the host's services, find none and note nothing.
/// </summary>
public interface IPluginContributionLog
{
    /// <summary>Notes one thing the plugin registers: its kind (<see cref="PluginContributionKinds"/>), its name and a detail.</summary>
    public void Add(string kind, string name, string? detail = null);
}

public static class PluginContributionLogExtensions
{
    /// <summary>Notes a contribution when <paramref name="services"/> are a plugin's; does nothing for core's.</summary>
    public static void Contributes(
        this IServiceProvider services,
        string kind,
        string name,
        string? detail = null
    ) =>
        (services.GetService(typeof(IPluginContributionLog)) as IPluginContributionLog)?.Add(
            kind,
            name,
            detail
        );
}

/// <summary>
/// The kinds of contribution the host notes. A registry a plugin adds may note kinds of its own.
/// </summary>
public static class PluginContributionKinds
{
    public const string CLIENT_MODULE = "client-module";
    public const string TEMPLATE_PATCH = "template-patch";

    /// <summary>A registration that replaces one already there (core's or another plugin's).</summary>
    public const string OVERRIDE = "override";
    public const string MESSAGE = "message";
    public const string MESSAGE_HANDLER = "message-handler";
    public const string CAPABILITY = "capability";
    public const string INCOMING_HEADER = "incoming-header";
    public const string OUTGOING_HEADER = "outgoing-header";
    public const string COMMAND = "command";
    public const string HANDLER = "handler";
    public const string BEHAVIOR = "behavior";
    public const string LISTENER = "listener";
    public const string FURNI_LOGIC = "furni-logic";
    public const string WIRED_BOX = "wired-box";
    public const string WIRED_VARIABLE = "wired-variable";
    public const string GAME = "game";
    public const string PLAY_POLICY = "play-policy";

    /// <summary>A host service the plugin replaces (<c>docs/plugin-services.md</c>): the contract, the class.</summary>
    public const string SERVICE = "service";
    public const string PERMISSION_SOURCE = "permission-source";
    public const string PERMISSION_NODE = "permission-node";
    public const string SETTING = "setting";
    public const string WEB_ROUTE = "web-route";

    /// <summary>A tool assistants call over MCP (<c>docs/plugin-mcp-tools.md</c>): its name, read-only or not and its nodes.</summary>
    public const string MCP_TOOL = "mcp-tool";
    public const string ADMIN_PAGE = "admin-page";
    public const string ADMIN_ENDPOINT = "admin-endpoint";
    public const string DATABASE = "database";
    public const string MIGRATION = "migration";
    public const string HOSTED_SERVICE = "hosted-service";
    public const string SCHEDULED_TASK = "scheduled-task";
}

/// <summary>One thing a plugin registers.</summary>
public sealed record PluginContribution(string Kind, string Name, string? Detail);

/// <summary>
/// A problem clients report with a plugin's client half (<see cref="ClientExtensionReportKinds"/>):
/// how many players' clients said so, and when one last did. <see cref="Reason"/> is the client's: for
/// a patch the element it is about (<c>credits_old</c>, <c>tab_button:bind</c>, <c>''.caption</c>) or
/// the ops applied, so the panel puts it beside the patch naming that element.
/// </summary>
public sealed record PluginClientIssue(
    string Kind,
    string Subject,
    string Reason,
    string Message,
    int Clients,
    DateTime LastReportedUtc
);

/// <summary>
/// Everything one plugin contributes, for the inspector: what its last load registered (a failed
/// load: what it had registered when it failed), the problems that load hit, clashes with another
/// live plugin, and what clients report.
/// </summary>
/// <param name="Conflicts">Each a sentence: a name another live plugin registers too, or a client-reported patch clash.</param>
public sealed record PluginContributions(
    string Key,
    string? Name,
    PluginStateType State,
    string? Error,
    ImmutableArray<PluginContribution> Items,
    ImmutableArray<string> Conflicts,
    ImmutableArray<PluginClientIssue> ClientIssues
);

/// <summary>What a client reports about an extension's client half (<c>TurboClientExtensionReportMessage</c>).</summary>
public static class ClientExtensionReportKinds
{
    /// <summary>The module was not run: its host is not allowed, its hash did not match, its file did not load.</summary>
    public const string MODULE_REFUSED = "module.refused";

    /// <summary>The module ran and threw (in <c>activate</c>).</summary>
    public const string MODULE_FAILED = "module.failed";

    /// <summary>A template patch names an element its template does not have.</summary>
    public const string PATCH_UNMATCHED = "patch.unmatched";

    /// <summary>Two owners set one property of one element; the later one's value is used.</summary>
    public const string PATCH_CONFLICT = "patch.conflict";

    /// <summary>The ops an owner's patches applied to a template (the subject), as a count.</summary>
    public const string PATCH_APPLIED = "patch.applied";

    /// <summary>
    /// A patch op matched its element but changes nothing the window draws (the window's code sets
    /// that field); the reason is <c>element.field</c>.
    /// </summary>
    public const string PATCH_INEFFECTIVE = "patch.ineffective";

    /// <summary>The reason a module was refused because of its origin: the hotel's setting names it.</summary>
    public const string HOST_NOT_ALLOWED = "host";

    public static readonly ImmutableHashSet<string> ALL =
    [
        MODULE_REFUSED,
        MODULE_FAILED,
        PATCH_UNMATCHED,
        PATCH_CONFLICT,
        PATCH_APPLIED,
        PATCH_INEFFECTIVE,
    ];
}

/// <summary>One line of a client's report: the extension (plugin key) it is about, what happened, to what.</summary>
/// <param name="Reason">For a refused module, why (<see cref="ClientExtensionReportKinds.HOST_NOT_ALLOWED"/>, ...).</param>
public sealed record ClientExtensionReport(
    string Owner,
    string Kind,
    string Subject,
    string Reason,
    string Message
);

/// <summary>
/// The client halves' reports, per plugin, kept in memory for the inspector: bounded per plugin,
/// counted by distinct players. A refused module is logged on the server too, naming what to change.
/// </summary>
public interface IClientExtensionReports
{
    public void Report(long playerId, ImmutableArray<ClientExtensionReport> reports);

    public ImmutableArray<PluginClientIssue> IssuesOf(string owner);
}
