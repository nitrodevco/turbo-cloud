using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Inventory.Badges;

internal class GetBadgeInfoMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetBadgeInfoMessage { BadgeCode = packet.PopString() };
}
