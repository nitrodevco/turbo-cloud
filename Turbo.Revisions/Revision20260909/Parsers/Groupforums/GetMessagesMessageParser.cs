using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, thread, start index, amount (AS3 GetMessagesMessageComposer).</summary>
internal class GetMessagesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetMessagesMessage
        {
            GroupId = packet.PopInt(),
            ThreadId = packet.PopInt(),
            StartIndex = packet.PopInt(),
            Amount = packet.PopInt(),
        };
}
