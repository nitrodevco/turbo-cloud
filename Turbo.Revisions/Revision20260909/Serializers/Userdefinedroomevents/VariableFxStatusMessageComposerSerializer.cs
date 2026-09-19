using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class VariableFxStatusMessageComposerSerializer(int header)
    : AbstractSerializer<VariableFxStatusMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, VariableFxStatusMessageComposer message)
    {
        packet.WriteBoolean(message.InitializeAll).WriteInteger(message.Statuses.Length);

        foreach (var status in message.Statuses)
        {
            // The client splits the key at the first '|': config id, then the variable id.
            packet
                .WriteString($"{status.Key.ConfigId}|{status.Key.VariableId}")
                .WriteBoolean(status.IsInitialize)
                .WriteBoolean(status.Key.IsUserEntity)
                .WriteInteger(status.Key.EntityId)
                .WriteLong(status.Value);

            // The client reads the two bounds as a pair or not at all.
            var hasOverrides =
                status.OverrideMinValue is not null && status.OverrideMaxValue is not null;

            packet.WriteBoolean(hasOverrides);

            if (hasOverrides)
                packet
                    .WriteLong(status.OverrideMinValue!.Value)
                    .WriteLong(status.OverrideMaxValue!.Value);

            packet.WriteInteger(status.Extra.Count);

            foreach (var (key, value) in status.Extra)
                packet.WriteString(key).WriteString(value);
        }
    }
}
