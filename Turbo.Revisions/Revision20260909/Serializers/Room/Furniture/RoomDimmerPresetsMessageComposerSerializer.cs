using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

/// <summary>
/// A moodlight's presets. <paramref name="keepFurnitureLights"/> is the layout a client with
/// <c>room_dimmer_lights</c> reads: each preset ends with whether furni keep their own lights
/// (build 116's <c>RoomDimmerPresetsMessageParser</c>). An older client reads no such boolean.
/// </summary>
internal class RoomDimmerPresetsMessageComposerSerializer(
    int header,
    bool keepFurnitureLights = false
) : AbstractSerializer<RoomDimmerPresetsMessageComposer>(header)
{
    private readonly bool _keepFurnitureLights = keepFurnitureLights;

    protected override void Serialize(
        IServerPacket packet,
        RoomDimmerPresetsMessageComposer message
    )
    {
        packet.WriteInteger(message.Presets.Length).WriteInteger(message.SelectedPresetId);

        foreach (var preset in message.Presets)
        {
            packet
                .WriteInteger(preset.Id)
                .WriteInteger(preset.Type)
                .WriteString(preset.Color)
                .WriteInteger(preset.Brightness);

            if (_keepFurnitureLights)
                packet.WriteBoolean(preset.KeepFurnitureLights);
        }

        packet.WriteBoolean(message.IsOn).WriteInteger(message.ItemId);
    }
}
