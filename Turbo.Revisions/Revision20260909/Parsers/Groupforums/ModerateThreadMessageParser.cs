using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, thread, new state (AS3 ModerateThreadMessageComposer).</summary>
internal class ModerateThreadMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ModerateThreadMessage
        {
            GroupId = packet.PopInt(),
            ThreadId = packet.PopInt(),
            State = packet.PopInt(),
        };
}
