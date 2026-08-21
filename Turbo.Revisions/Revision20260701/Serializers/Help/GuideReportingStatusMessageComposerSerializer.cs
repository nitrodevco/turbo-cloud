using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class GuideReportingStatusMessageComposerSerializer(int header)
    : AbstractSerializer<GuideReportingStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuideReportingStatusMessageComposer message
    )
    {
        //
    }
}
