using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class RoomDimmerSavePresetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var presetId = packet.PopInt();
        var effectType = packet.PopInt();
        var color = packet.PopString();
        var brightness = packet.PopInt();
        var apply = packet.PopBoolean();

        // Whether furni keep their own lights (build 116's RoomDimmerSavePresetMessageComposer);
        // an older client always writes false here.
        var keepFurnitureLights = packet.PopBoolean();

        return new RoomDimmerSavePresetMessage
        {
            PresetId = presetId,
            EffectType = effectType,
            Color = color,
            Brightness = brightness,
            Apply = apply,
            KeepFurnitureLights = keepFurnitureLights,
            ObjectId = packet.PopInt(),
        };
    }
}
