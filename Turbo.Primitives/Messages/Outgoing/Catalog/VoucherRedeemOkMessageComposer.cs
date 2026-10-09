using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// A voucher redeemed: the furniture it gave, named for the client's alert, or both empty when it
/// gave none.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VoucherRedeemOkMessageComposer : IComposer
{
    [Id(0)]
    public required string ProductName { get; init; }

    [Id(1)]
    public required string ProductDescription { get; init; }
}
