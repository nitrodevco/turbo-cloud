using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class SetMannequinFigureMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SetMannequinFigureMessage { ObjectId = packet.PopInt() };
}
