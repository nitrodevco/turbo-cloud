using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.FriendList;

/// <summary>The chat id, the Habbicon, then the confirmation id (AS3 <c>_-Vl</c>, <c>MainView.onHabbiconSelected</c>).</summary>
internal class SendHabbiconMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SendHabbiconMessage
        {
            ChatId = packet.PopInt(),
            HabbiconId = packet.PopInt(),
            ConfirmationId = packet.PopInt(),
        };
}
