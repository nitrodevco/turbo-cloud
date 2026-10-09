using Turbo.Primitives.Packets;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>A quest in the order the AS3 <c>QuestMessageData</c> reads it.</summary>
internal static class QuestSerializer
{
    public static void Write(IServerPacket packet, QuestSnapshot quest)
    {
        packet.WriteString(quest.CampaignCode);
        packet.WriteInteger(quest.CompletedQuestsInCampaign);
        packet.WriteInteger(quest.QuestCountInCampaign);
        packet.WriteInteger(quest.ActivityPointType);
        packet.WriteInteger(quest.Id);
        packet.WriteBoolean(quest.Accepted);
        packet.WriteString(quest.Type);
        packet.WriteString(quest.ImageVersion);
        packet.WriteInteger(quest.RewardCurrencyAmount);
        packet.WriteString(quest.LocalizationCode);
        packet.WriteInteger(quest.CompletedSteps);
        packet.WriteInteger(quest.TotalSteps);
        packet.WriteInteger(quest.SortOrder);
        packet.WriteString(quest.CatalogPageName);
        packet.WriteString(quest.ChainCode);
        packet.WriteBoolean(quest.Easy);
        packet.WriteBoolean(quest.IsSeasonal);

        if (quest.IsSeasonal)
            packet.WriteInteger(quest.SecondsLeft);
    }
}
