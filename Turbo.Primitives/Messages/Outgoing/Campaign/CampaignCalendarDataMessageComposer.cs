using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Campaign;

/// <summary>
/// The calendar the client opens on <c>openView/calendar</c> (AS3 CampaignCalendarData.parse):
/// its name and image, today's door, how many doors, the doors opened and the doors missed.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CampaignCalendarDataMessageComposer : IComposer
{
    [Id(0)]
    public required string CampaignName { get; init; }

    [Id(1)]
    public required string CampaignImage { get; init; }

    [Id(2)]
    public required int CurrentDay { get; init; }

    [Id(3)]
    public required int CampaignDays { get; init; }

    [Id(4)]
    public required ImmutableArray<int> OpenedDays { get; init; }

    [Id(5)]
    public required ImmutableArray<int> MissedDays { get; init; }
}
