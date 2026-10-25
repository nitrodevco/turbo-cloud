using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Campaigns;

/// <summary>
/// The campaign calendars (<c>docs/campaign-calendar.md</c>): what a player's calendar shows, and
/// opening its doors. A plugin replaces it by registering its own.
/// </summary>
public interface ICampaignCalendarService
{
    /// <summary>
    /// The player's calendar of the live campaign (the latest started when several are), or null
    /// when none is live. Habbo's client never asks for it: it is sent at login.
    /// </summary>
    public Task<CampaignCalendarDataMessageComposer?> GetCalendarAsync(
        PlayerId playerId,
        CancellationToken ct
    );

    /// <summary>
    /// Opens a door of the live campaign <paramref name="campaignName"/> and gives what it holds,
    /// once per player and door. A player opens only today's door; <paramref name="asStaff"/>
    /// (the staff force open, checked by the caller) opens any of its days.
    /// </summary>
    public Task<CampaignCalendarDoorOpenedMessageComposer> OpenDoorAsync(
        PlayerId playerId,
        string campaignName,
        int dayIndex,
        bool asStaff,
        CancellationToken ct
    );

    public Task<ImmutableArray<CampaignCalendarDefinition>> ListAsync(CancellationToken ct);

    public Task<CampaignCalendarDefinition?> GetAsync(int id, CancellationToken ct);

    /// <summary>Saves a campaign and its rewards (id 0 adds one). Throws <see cref="System.ArgumentException"/> for an invalid one.</summary>
    public Task<CampaignCalendarDefinition> SaveAsync(
        CampaignCalendarDefinition campaign,
        CancellationToken ct
    );

    /// <summary>Removes a campaign, its rewards and who opened what.</summary>
    public Task<bool> DeleteAsync(int id, CancellationToken ct);
}
