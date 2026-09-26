using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Action;

internal class RemoveRightsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RemoveRightsMessage
        {
            PlayerIds = packet.PopList(bytesPerItem: 4, p => PlayerId.Parse(p.PopInt())),
        };
}
