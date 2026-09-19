using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Engine;

internal class SetClothingChangeDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetClothingChangeDataMessage
        {
            ObjectId = packet.PopInt(),
            Gender = packet.PopString(),
            Figure = packet.PopString(),
        };
}
