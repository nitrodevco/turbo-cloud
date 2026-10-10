using System.Collections.Immutable;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>A count, then per forum the group id, last read message id and mark-all flag (AS3 UpdateForumReadMarkerMessageComposer.add).</summary>
internal class UpdateForumReadMarkerMessageParser : IParser
{
    // Two ints and a boolean.
    private const int BYTES_PER_MARKER = 9;

    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateForumReadMarkerMessage
        {
            Markers =
            [
                .. packet.PopList(
                    BYTES_PER_MARKER,
                    static p => new GuildForumReadMarkerSnapshot
                    {
                        GroupId = p.PopInt(),
                        LastReadMessageId = p.PopInt(),
                        MarkAll = p.PopBoolean(),
                    }
                ),
            ],
        };
}
