using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// Somewhere the hotel keeps what a plugin put there that outlives its code: its tables, the
/// panel's overrides of its settings, the grants of its permission nodes. Uninstalling a plugin
/// asks each one what it holds of the plugin (the dry run), and with "delete its data" has each
/// take it away. Each owner finds and removes only what is the plugin's own by
/// <see cref="PluginDataScope"/>, never anything of the hotel's or of another plugin.
/// </summary>
public interface IPluginDataOwner
{
    /// <summary>What it holds of the plugin now, with how much of each.</summary>
    public Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    );

    /// <summary>
    /// Takes away <paramref name="found"/>: what <see cref="FindAsync"/> answered for the plugin,
    /// while it was still loaded. Called after the plugin is unloaded; <paramref name="actor"/> is
    /// who uninstalled it, for the owner's own history.
    /// </summary>
    public Task RemoveAsync(
        PluginDataScope plugin,
        ImmutableArray<PluginDataItem> found,
        PlayerId actor,
        CancellationToken ct
    );
}

/// <summary>
/// The plugin whose data is asked for. <see cref="TablePrefix"/> is its manifest's, null when it
/// has none; <see cref="OtherTablePrefixes"/> are those of every other plugin installed, so a table
/// of one whose prefix starts with this one's ("bug_" and "bug_report_") is never taken for it.
/// <see cref="NodeRoots"/> are the first segments of its permission nodes (<c>plugin.&lt;key&gt;</c>,
/// and its key, as <c>IPermissionNodeSource.Prefix</c> is the plugin id), none of them core's.
/// <see cref="Folder"/> is where it lies on disk, null when that is not known.
/// </summary>
public sealed record PluginDataScope(
    string Key,
    string? TablePrefix,
    ImmutableArray<string> OtherTablePrefixes,
    ImmutableArray<string> NodeRoots,
    string? Folder = null
);

/// <summary>What kind of data an item is.</summary>
public enum PluginDataKind
{
    /// <summary>One of its tables, or its migrations' history; <see cref="PluginDataItem.Count"/> is its rows.</summary>
    Table,

    /// <summary>A setting of its overridden in the panel; <see cref="PluginDataItem.Detail"/> is the value, null for a secret.</summary>
    Setting,

    /// <summary>One of its nodes or meta keys given to groups or players; <see cref="PluginDataItem.Count"/> is how many.</summary>
    Grant,

    /// <summary>An external variable it brought, still as it gave it; <see cref="PluginDataItem.Detail"/> is the value.</summary>
    Variable,

    /// <summary>An external text it brought, still as it gave it; <see cref="PluginDataItem.Detail"/> is the value.</summary>
    Text,
}

/// <summary>One thing a plugin left: its kind, its name (a table, a setting's path, a node), how many rows or holders, and what to show of it.</summary>
public sealed record PluginDataItem(
    PluginDataKind Kind,
    string Name,
    long Count,
    string? Detail = null
);
