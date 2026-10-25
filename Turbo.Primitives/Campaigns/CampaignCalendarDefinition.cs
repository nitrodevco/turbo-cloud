using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.Campaigns;

/// <summary>
/// A campaign calendar: one door a day for <see cref="Days"/> days from <see cref="StartsAt"/>.
/// <see cref="Name"/> is what the client sends back when a door is opened and the key of its
/// title text (<c>campaign.calendar.&lt;name&gt;.title</c>); <see cref="Image"/> is sent with it
/// (<c>CampaignCalendarData.campaignImage</c>).
/// </summary>
public sealed record CampaignCalendarDefinition
{
    public int Id { get; init; }

    public required string Name { get; init; }

    public string Image { get; init; } = string.Empty;

    /// <summary>When day 0's door opens, in UTC; each next door opens a day later.</summary>
    public DateTime StartsAt { get; init; }

    public int Days { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>What each day's door gives, by day index; a day without one has nothing to give.</summary>
    public ImmutableArray<CampaignCalendarRewardDefinition> Rewards { get; init; } = [];
}
