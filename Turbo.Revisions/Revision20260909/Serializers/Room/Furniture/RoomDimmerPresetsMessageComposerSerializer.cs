using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

internal class RoomDimmerPresetsMessageComposerSerializer(int header)
    : AbstractSerializer<RoomDimmerPresetsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomDimmerPresetsMessageComposer message
    )
    {
        packet.WriteInteger(message.Presets.Length).WriteInteger(message.SelectedPresetId);

        foreach (var preset in message.Presets)
            packet
                .WriteInteger(preset.Id)
                .WriteInteger(preset.Type)
                .WriteString(preset.Color)
                .WriteInteger(preset.Brightness);

        packet.WriteBoolean(message.IsOn).WriteInteger(message.ItemId);
    }
}
