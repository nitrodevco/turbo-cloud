using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Engine;

internal class RemoveBotFromFlatMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RemoveBotFromFlatMessage { BotId = packet.PopInt() };
}
