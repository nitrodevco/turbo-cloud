using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

/// <summary>
/// Starts a quest campaign by its code: the tracker's default campaign for a new identity
/// (<c>questing.defaultCampaign</c>) or the VIP quests promo's (<c>citizenship.vip.tutorial.quest.campaign.name</c>),
/// <c>StartCampaignMessageComposer(campaignName)</c>.
/// </summary>
public record StartCampaignMessage : IMessageEvent
{
    public required string CampaignCode { get; init; }
}
