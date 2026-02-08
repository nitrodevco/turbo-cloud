using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class GuideOnDutyStatusMessageComposerSerializer(int header)
    : AbstractSerializer<GuideOnDutyStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideOnDutyStatusMessageComposer message
    )
    {
        //
    }
}
