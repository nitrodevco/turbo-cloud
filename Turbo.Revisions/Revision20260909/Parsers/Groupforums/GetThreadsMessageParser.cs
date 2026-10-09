using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, start index, amount (AS3 GetThreadsMessageComposer).</summary>
internal class GetThreadsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetThreadsMessage
        {
            GroupId = packet.PopInt(),
            StartIndex = packet.PopInt(),
            Amount = packet.PopInt(),
        };
}
