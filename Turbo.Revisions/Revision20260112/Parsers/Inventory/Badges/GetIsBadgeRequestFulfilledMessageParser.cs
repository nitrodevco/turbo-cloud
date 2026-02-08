using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Inventory.Badges;

internal class GetIsBadgeRequestFulfilledMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetIsBadgeRequestFulfilledMessage();
}
