using Turbo.Primitives.Messages.Incoming.Room.Pets;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Pets;

internal class RespectPetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new RespectPetMessage();
}
