using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class BuildersClubPlacementWarningMessageComposerSerializer(int header)
    : AbstractSerializer<BuildersClubPlacementWarningMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BuildersClubPlacementWarningMessageComposer message
    )
    {
        //
    }
}
