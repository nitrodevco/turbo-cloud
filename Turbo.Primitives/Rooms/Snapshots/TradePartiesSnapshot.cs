using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Snapshots;

/// <summary>
/// The room's answer to "may these two trade here": who the partner standing at the clicked
/// avatar is, and whether the room's trade mode and rights let each side trade.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TradePartiesSnapshot
{
    [Id(0)]
    public required PlayerId InitiatorId { get; init; }

    [Id(1)]
    public required PlayerId PartnerId { get; init; }

    [Id(2)]
    public required string PartnerName { get; init; }

    [Id(3)]
    public required bool InitiatorMayTrade { get; init; }

    [Id(4)]
    public required bool PartnerMayTrade { get; init; }
}
