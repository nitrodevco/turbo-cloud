using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Availability;

internal class MaintenanceStatusMessageComposerSerializer(int header)
    : AbstractSerializer<MaintenanceStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MaintenanceStatusMessageComposer message
    )
    {
        //
    }
}
