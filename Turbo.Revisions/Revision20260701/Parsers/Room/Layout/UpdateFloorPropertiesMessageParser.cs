using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Layout;

internal class UpdateFloorPropertiesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new UpdateFloorPropertiesMessage();
}
