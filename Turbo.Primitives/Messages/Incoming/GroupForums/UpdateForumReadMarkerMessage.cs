using System.Collections.Immutable;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Groupforums;

public record UpdateForumReadMarkerMessage : IMessageEvent
{
    public required ImmutableArray<GuildForumReadMarkerSnapshot> Markers { get; init; }
}
