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

        // The client always writes false here; nothing reads it.
        packet.PopBoolean();

        return new RoomDimmerSavePresetMessage
        {
            PresetId = presetId,
            EffectType = effectType,
            Color = color,
            Brightness = brightness,
            Apply = apply,
            ObjectId = packet.PopInt(),
        };
    }
}
