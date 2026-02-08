using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class AuthenticationOKMessageSerializer(int header)
    : AbstractSerializer<AuthenticationOKMessage>(header)
{
    protected override void Serialize(IServerPacket packet, AuthenticationOKMessage message)
    {
        packet.WriteInteger(message.AccountId);
        packet.WriteInteger(message.SuggestedLoginActions?.Length ?? 0);

        if (message.SuggestedLoginActions != null)
        {
            foreach (var action in message.SuggestedLoginActions)
            {
                packet.WriteShort(action);
            }
        }

        packet.WriteInteger(message.IdentityId);
    }
}
