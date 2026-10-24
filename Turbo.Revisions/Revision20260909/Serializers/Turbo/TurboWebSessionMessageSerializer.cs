using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboWebSessionMessageSerializer(int header)
    : AbstractSerializer<TurboWebSessionMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboWebSessionMessage message) =>
        packet
            .WriteString(message.BaseUrl)
            .WriteString(message.Token)
            .WriteInteger(message.ExpiresInSeconds);
}
