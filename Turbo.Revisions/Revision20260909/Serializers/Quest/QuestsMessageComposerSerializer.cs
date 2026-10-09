using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

internal class QuestsMessageComposerSerializer(int header)
    : AbstractSerializer<QuestsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestsMessageComposer message)
    {
        packet.WriteInteger(message.Quests.Length);

        foreach (var quest in message.Quests)
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

        packet.WriteBoolean(message.OpenWindow);
    }
}
