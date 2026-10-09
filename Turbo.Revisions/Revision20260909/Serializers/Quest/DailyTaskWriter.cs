using System.Collections.Immutable;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>
/// A count of tasks, each in the order AS3 <c>DailyTaskInfo</c> reads it: task id (long), task
/// code, quest type code, bonus, image version, catalog name, required repeats, repeats, status
/// (byte), seconds left, then a count of rewards, each product type (short), reward type id,
/// extra params and amount.
/// </summary>
internal static class DailyTaskWriter
{
    public static void WriteTasks(IServerPacket packet, ImmutableArray<DailyTaskSnapshot> tasks)
    {
        packet.WriteInteger(tasks.Length);

        foreach (var task in tasks)
        {
            packet.WriteLong(task.TaskId);
            packet.WriteString(task.TaskCode);
            packet.WriteString(task.QuestTypeCode);
            packet.WriteBoolean(task.IsBonus);
            packet.WriteString(task.ImageVersion);
            packet.WriteString(task.CatalogName);
            packet.WriteInteger(task.RequiredRepeats);
            packet.WriteInteger(task.Repeats);
            packet.WriteByte((byte)task.Status);
            packet.WriteInteger(task.SecondsLeft);
            packet.WriteInteger(task.Rewards.Length);

            foreach (var reward in task.Rewards)
            {
                packet.WriteShort((short)reward.ProductType);
                packet.WriteString(reward.RewardTypeId);
                packet.WriteString(reward.ExtraParams);
                packet.WriteInteger(reward.Amount);
            }
        }
    }
}
