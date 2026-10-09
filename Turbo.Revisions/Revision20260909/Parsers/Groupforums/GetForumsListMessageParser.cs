using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Groupforums;

/// <summary>List code, start index, amount (AS3 GetForumsListMessageComposer).</summary>
internal class GetForumsListMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetForumsListMessage
        {
            ListCode = packet.PopInt(),
            StartIndex = packet.PopInt(),
            Amount = packet.PopInt(),
        };
}
