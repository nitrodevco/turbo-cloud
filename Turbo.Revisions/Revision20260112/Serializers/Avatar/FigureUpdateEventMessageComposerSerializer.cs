using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260112.Serializers.Avatar;

internal class FigureUpdateEventMessageComposerSerializer(int header)
    : AbstractSerializer<FigureUpdateEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FigureUpdateEventMessageComposer message
    )
    {
        packet.WriteString(message.Figure).WriteString(message.Gender.ToLegacyString());
    }
}
