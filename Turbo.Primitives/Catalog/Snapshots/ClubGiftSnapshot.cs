using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// What the client needs to draw one club gift beside its offer: how long a member must have
/// been one to deserve it, and whether this member may pick it right now.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ClubGiftSnapshot
{
    [Id(0)]
    public required int OfferId { get; init; }

    /// <summary>
    /// Always false. The client measures a VIP gift against the player's VIP days alone, and
    /// this hotel keeps no separate VIP tally (<see cref="ClubOfferSnapshot.IsVip"/> says why),
    /// so a gift marked VIP would read as for ever out of reach.
    /// </summary>
    [Id(1)]
    public required bool IsVip { get; init; }

    [Id(2)]
    public required int DaysRequired { get; init; }

    /// <summary>
    /// The server's verdict, which is what the client draws the button from: this member has a
    /// gift to spend and has been a member long enough for this one.
    /// </summary>
    [Id(3)]
    public required bool IsSelectable { get; init; }
}
