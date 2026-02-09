using Turbo.Primitives.Messages.Incoming.Register;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Register;

internal class UpdateFigureDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateFigureDataMessage { Gender = packet.PopString(), Figure = packet.PopString() };
}
