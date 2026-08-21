using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Inventory.Badges;

public class GetBadgesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetBadgesMessage();
}
