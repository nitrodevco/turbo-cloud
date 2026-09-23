using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Layout;

internal class UpdateFloorPropertiesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var modelData = packet.PopString();

        // The editor sends one field, six, or seven, depending on what it has to say
        // (UpdateFloorPropertiesMessageComposer picks between the three).
        if (packet.End)
            return new UpdateFloorPropertiesMessage { ModelData = modelData };

        return new UpdateFloorPropertiesMessage
        {
            ModelData = modelData,
            HasProperties = true,
            DoorX = packet.PopInt(),
            DoorY = packet.PopInt(),
            DoorRotation = packet.PopInt(),
            WallThickness = packet.PopInt(),
            FloorThickness = packet.PopInt(),
            FixedWallsHeight = packet.End ? -1 : packet.PopInt(),
        };
    }
}
