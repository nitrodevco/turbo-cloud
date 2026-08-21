using Turbo.Primitives.Messages.Incoming.Inventory.Bots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Inventory.Bots;

internal class GetBotInventoryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetBotInventoryMessage();
}
