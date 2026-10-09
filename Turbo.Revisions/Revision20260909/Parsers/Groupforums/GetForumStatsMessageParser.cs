using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>The group id (AS3 GetForumStatsMessageComposer).</summary>
internal class GetForumStatsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetForumStatsMessage { GroupId = packet.PopInt() };
}
