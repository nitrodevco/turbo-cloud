using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>Group, thread (AS3 GetThreadMessageComposer).</summary>
internal class GetThreadMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetThreadMessage { GroupId = packet.PopInt(), ThreadId = packet.PopInt() };
}
