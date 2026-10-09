using System;
using System.Collections.Immutable;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>A count, then per forum the group id, last read message id and mark-all flag (AS3 UpdateForumReadMarkerMessageComposer.add).</summary>
internal class UpdateForumReadMarkerMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var count = packet.PopInt();
        var markers = ImmutableArray.CreateBuilder<GuildForumReadMarkerSnapshot>(
            Math.Max(0, count)
        );

        for (var i = 0; i < count; i++)
            markers.Add(
                new GuildForumReadMarkerSnapshot
                {
                    GroupId = packet.PopInt(),
                    LastReadMessageId = packet.PopInt(),
                    MarkAll = packet.PopBoolean(),
                }
            );

        return new UpdateForumReadMarkerMessage { Markers = markers.ToImmutable() };
    }
}
