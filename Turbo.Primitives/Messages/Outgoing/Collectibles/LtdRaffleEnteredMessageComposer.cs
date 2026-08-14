using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>
/// Sent when a player successfully enters the LTD raffle queue.
/// Shows "entering raffle" UI with dots animation in purchase confirmation.
/// Header ID: 1221
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LtdRaffleEnteredMessageComposer : IComposer
{
    /// <summary>
    /// The product name/type for which the user entered the raffle.
    /// </summary>
    [Id(0)]
    public required string ClassName { get; init; }
}
