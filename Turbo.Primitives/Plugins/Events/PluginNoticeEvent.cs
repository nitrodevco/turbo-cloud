using System.Collections.Immutable;
using Turbo.Primitives.Events;

namespace Turbo.Primitives.Plugins.Events;

/// <summary>
/// Something a plugin tells whoever relays notices (a Discord plugin, a log), without knowing who
/// does or whether anyone is loaded: a new client error, a bug report, a room copy finished. Each
/// plugin loads in its own load context, so two plugins share only the host's types; this one is
/// the host's. Publish it with <c>EventSystem.PublishAsync</c> (taken from the host services) and
/// handle it with an <c>IEventHandler&lt;PluginNoticeEvent&gt;</c>. A relay routes by
/// <see cref="Source"/> and <see cref="Topic"/>, and passes on the texts as they are. See
/// <c>docs/plugin-notices.md</c>.
/// </summary>
public sealed record PluginNoticeEvent : IEvent
{
    public const int MAX_TOPIC_LENGTH = 64;
    public const int MAX_TITLE_LENGTH = 256;
    public const int MAX_TEXT_LENGTH = 4000;
    public const int MAX_FIELDS = 20;

    /// <summary>The key of the plugin that raised it, as in its manifest (<c>clienterrors</c>).</summary>
    public required string Source { get; init; }

    /// <summary>
    /// What happened, a stable code of the source's own (<c>issue.new</c>); at most
    /// <see cref="MAX_TOPIC_LENGTH"/> characters. A relay lists each topic it hears to be routed.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>One line: what a reader sees first. At most <see cref="MAX_TITLE_LENGTH"/> characters.</summary>
    public required string Title { get; init; }

    /// <summary>More, in plain text; at most <see cref="MAX_TEXT_LENGTH"/> characters.</summary>
    public string? Text { get; init; }

    /// <summary>Where to read more: an admin panel page, a room link.</summary>
    public string? Url { get; init; }

    /// <summary>Named values (a player, a build, a count), in order; at most <see cref="MAX_FIELDS"/>.</summary>
    public ImmutableArray<PluginNoticeField> Fields { get; init; } = [];

    public PluginNoticeLevel Level { get; init; } = PluginNoticeLevel.Info;

    /// <summary>
    /// Names the thing the notice is about (an issue id): a relay passes on one notice per source,
    /// topic and key, so a notice raised again for the same thing (a retry, a second server) is
    /// not posted twice. Null: every notice is its own.
    /// </summary>
    public string? Key { get; init; }
}

/// <summary>A named value of a <see cref="PluginNoticeEvent"/>.</summary>
public readonly record struct PluginNoticeField(string Name, string Value);

/// <summary>How a relay may show a notice (a colour, an icon).</summary>
public enum PluginNoticeLevel
{
    Info,
    Success,
    Warning,
    Error,
}
