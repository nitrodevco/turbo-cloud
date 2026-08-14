using Orleans;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// Result of attempting to enter an LTD raffle.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record LtdRaffleEntryResult
{
    [Id(0)]
    public required bool Success { get; init; }

    [Id(1)]
    public required string BatchId { get; init; }

    [Id(2)]
    public required LtdRaffleEntryErrorType Error { get; init; }

    [Id(3)]
    public CatalogBalanceFailure? BalanceFailure { get; init; }

    public static LtdRaffleEntryResult Succeeded(string batchId) =>
        new()
        {
            Success = true,
            BatchId = batchId,
            Error = LtdRaffleEntryErrorType.None,
            BalanceFailure = null,
        };

    public static LtdRaffleEntryResult Failed(
        LtdRaffleEntryErrorType error,
        CatalogBalanceFailure? balanceFailure = null
    ) =>
        new()
        {
            Success = false,
            BatchId = "",
            Error = error,
            BalanceFailure = balanceFailure,
        };
}
