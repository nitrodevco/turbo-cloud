using Turbo.Primitives.Messages.Outgoing.Game.Lobby;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Lobby;

internal class AchievementResolutionProgressMessageComposerSerializer(int header)
    : AbstractSerializer<AchievementResolutionProgressMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AchievementResolutionProgressMessageComposer message
    )
    {
        //
    }
}
