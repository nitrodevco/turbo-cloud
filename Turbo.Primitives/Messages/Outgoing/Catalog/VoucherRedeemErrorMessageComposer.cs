using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>Why a voucher was not redeemed, as the client's alert texts number it.</summary>
[GenerateSerializer, Immutable]
public sealed record VoucherRedeemErrorMessageComposer : IComposer
{
    [Id(0)]
    public required VoucherRedeemErrorType Error { get; init; }
}
