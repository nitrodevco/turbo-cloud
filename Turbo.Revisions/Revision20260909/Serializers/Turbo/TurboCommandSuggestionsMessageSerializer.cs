using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboCommandSuggestionsMessageSerializer(int header)
    : AbstractSerializer<TurboCommandSuggestionsMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboCommandSuggestionsMessage message)
    {
        packet.WriteInteger(message.RequestId).WriteInteger(message.Values.Length);

        foreach (var value in message.Values)
            packet.WriteString(value);
    }
}
