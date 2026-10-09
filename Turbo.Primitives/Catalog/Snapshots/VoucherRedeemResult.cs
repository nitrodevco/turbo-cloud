using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// What redeeming a voucher came to: its rewards given, with the furniture's name and description
/// for the client's alert (empty when it gave none), or why not.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VoucherRedeemResult
{
    /// <summary>Null when it was redeemed.</summary>
    [Id(0)]
    public VoucherRedeemErrorType? Error { get; init; }

    [Id(1)]
    public string ProductName { get; init; } = string.Empty;

    [Id(2)]
    public string ProductDescription { get; init; } = string.Empty;

    public static VoucherRedeemResult Failed(VoucherRedeemErrorType error) =>
        new() { Error = error };
}
