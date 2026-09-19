using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class VariableFxConfigsMessageComposerSerializer(int header)
    : AbstractSerializer<VariableFxConfigsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        VariableFxConfigsMessageComposer message
    )
    {
        packet.WriteInteger(message.Configs.Length);

        foreach (var config in message.Configs)
        {
            packet
                .WriteInteger(config.ConfigId)
                .WriteBoolean(config.IsUserFx)
                .WriteInteger((int)config.ShowMode)
                .WriteInteger(config.UpdateMask)
                .WriteBoolean(config.ShowOnMouseHover)
                .WriteInteger(config.ShowDurationMs)
                .WriteInteger((int)config.Category)
                .WriteInteger(config.StyleId)
                .WriteInteger(config.ColorId)
                .WriteInteger(config.WidthId)
                .WriteInteger(config.RendererId)
                .WriteLong(config.DefaultMinValue)
                .WriteLong(config.DefaultMaxValue)
                .WriteInteger(config.Extra.Count);

            foreach (var (key, value) in config.Extra)
                packet.WriteString(key).WriteString(value);
        }
    }
}
