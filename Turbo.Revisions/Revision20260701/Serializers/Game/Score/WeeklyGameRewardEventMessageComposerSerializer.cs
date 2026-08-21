using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Score;

internal class WeeklyGameRewardEventMessageComposerSerializer(int header)
    : AbstractSerializer<WeeklyGameRewardEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WeeklyGameRewardEventMessageComposer message
    )
    {
        //
    }
}
