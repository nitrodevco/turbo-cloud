using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>
/// In the order AS3 RewardTracksMessageParser reads it: disabled, a count of tracks, reload.
/// A track: id, theme, points, whether it has premium and, only then, the task points boost
/// (double), instant points, diamond and credit costs; then owned premium, complete, premium
/// complete, a count of tasks (id, action type, parameter, progress, premium, a count of levels:
/// required count, points, premium) and a count of prizes (id, required points, product type
/// as a short, reward type id, extra params, amount, premium, available, claimed).
/// </summary>
internal class RewardTracksMessageComposerSerializer(int header)
    : AbstractSerializer<RewardTracksMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RewardTracksMessageComposer message)
    {
        packet.WriteBoolean(message.Disabled);
        packet.WriteInteger(message.Tracks.Length);

        foreach (var track in message.Tracks)
        {
            packet.WriteString(track.Id);
            packet.WriteString(track.Theme);
            packet.WriteInteger(track.Points);
            packet.WriteBoolean(track.PremiumConfig is not null);

            if (track.PremiumConfig is { } premium)
            {
                packet.WriteDouble(premium.TaskPointsBoost);
                packet.WriteInteger(premium.InstantPoints);
                packet.WriteInteger(premium.CostDiamonds);
                packet.WriteInteger(premium.CostCredits);
            }

            packet.WriteBoolean(track.Premium);
            packet.WriteBoolean(track.Complete);
            packet.WriteBoolean(track.PremiumComplete);
            packet.WriteInteger(track.Tasks.Length);

            foreach (var task in track.Tasks)
            {
                packet.WriteString(task.Id);
                packet.WriteString(task.ActionType);
                packet.WriteString(task.Parameter);
                packet.WriteInteger(task.ProgressCount);
                packet.WriteBoolean(task.Premium);
                packet.WriteInteger(task.Levels.Length);

                foreach (var level in task.Levels)
                {
                    packet.WriteInteger(level.RequiredCount);
                    packet.WriteInteger(level.PointsReward);
                    packet.WriteBoolean(level.Premium);
                }
            }

            packet.WriteInteger(track.Prizes.Length);

            foreach (var prize in track.Prizes)
            {
                packet.WriteString(prize.Id);
                packet.WriteInteger(prize.RequiredPoints);
                packet.WriteShort((short)prize.ProductType);
                packet.WriteString(prize.RewardTypeId);
                packet.WriteString(prize.ExtraParams);
                packet.WriteInteger(prize.Amount);
                packet.WriteBoolean(prize.Premium);
                packet.WriteBoolean(prize.Available);
                packet.WriteBoolean(prize.Claimed);
            }
        }

        packet.WriteBoolean(message.Reload);
    }
}
