using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Layout;

internal class GetOccupiedTilesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetOccupiedTilesMessage();
}
