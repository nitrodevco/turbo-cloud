using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Avatar;

internal class CheckUserNameResultMessageComposerSerializer(int header)
    : AbstractSerializer<CheckUserNameResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CheckUserNameResultMessageComposer message
    )
    {
        packet.WriteInteger((int)message.Result);
        packet.WriteString(message.Name);
        packet.WriteInteger(message.Suggestions.Length);

        foreach (var suggestion in message.Suggestions)
            packet.WriteString(suggestion);
    }
}
