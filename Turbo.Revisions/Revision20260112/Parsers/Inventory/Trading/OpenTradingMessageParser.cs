using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Inventory.Trading;

internal class OpenTradingMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new OpenTradingMessage();
}
