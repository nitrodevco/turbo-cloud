using Turbo.Primitives.Messages.Outgoing.Game.Lobby;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Lobby;

internal class AchievementResolutionCompletedMessageComposerSerializer(int header)
    : AbstractSerializer<AchievementResolutionCompletedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AchievementResolutionCompletedMessageComposer message
    )
    {
        //
    }
}
