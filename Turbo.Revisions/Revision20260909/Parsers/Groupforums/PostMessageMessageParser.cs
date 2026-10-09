using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, thread (0 starts one), subject, text (AS3 PostMessageMessageComposer).</summary>
internal class PostMessageMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new PostMessageMessage
        {
            GroupId = packet.PopInt(),
            ThreadId = packet.PopInt(),
            Subject = packet.PopString(),
            Text = packet.PopString(),
        };
}
