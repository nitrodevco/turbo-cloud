using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class RoomVisualizationSettingsMessageComposerSerializer(int header)
    : AbstractSerializer<RoomVisualizationSettingsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomVisualizationSettingsMessageComposer message
    )
    {
        //
    }
}
