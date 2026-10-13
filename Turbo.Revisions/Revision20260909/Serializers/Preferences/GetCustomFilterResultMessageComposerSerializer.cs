using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Preferences;

internal class GetCustomFilterResultMessageComposerSerializer(int header)
    : AbstractSerializer<GetCustomFilterResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GetCustomFilterResultMessageComposer message
    )
    {
        packet.WriteInteger(message.Words.Count);

        foreach (var word in message.Words)
            packet.WriteString(word);
    }
}
