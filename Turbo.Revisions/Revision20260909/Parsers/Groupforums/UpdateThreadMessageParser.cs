using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, thread, sticky, locked: the AS3 UpdateThreadMessageComposer puts sticky before locked.</summary>
internal class UpdateThreadMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateThreadMessage
        {
            GroupId = packet.PopInt(),
            ThreadId = packet.PopInt(),
            IsSticky = packet.PopBoolean(),
            IsLocked = packet.PopBoolean(),
        };
}
