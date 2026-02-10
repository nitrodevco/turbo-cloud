using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Collectibles;

/// <summary>
/// Sent when the LTD raffle completes with the player's result.
/// Header ID: 785
/// ResultCode 0 = Won (triggers notification.raffle.won)
/// ResultCode 1-3 = Lost (triggers notification.raffle.lost)
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LtdRaffleResultMessageComposer : IComposer
{
    /// <summary>
    /// The product name.
    /// </summary>
    [Id(0)]
    public required string ClassName { get; init; }

    /// <summary>
    /// The raffle result code.
    /// 0 = Won, 1+ = Lost
    /// </summary>
    [Id(1)]
    public required LtdRaffleResultCode ResultCode { get; init; }
}
