using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, thread, message, new state (AS3 ModerateMessageMessageComposer).</summary>
internal class ModerateMessageMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ModerateMessageMessage
        {
            GroupId = packet.PopInt(),
            ThreadId = packet.PopInt(),
            MessageId = packet.PopInt(),
            State = packet.PopInt(),
        };
}
