using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class GuideSessionInvitedToGuideRoomMessageComposerSerializer(int header)
    : AbstractSerializer<GuideSessionInvitedToGuideRoomMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideSessionInvitedToGuideRoomMessageComposer message
    )
    {
        //
    }
}
