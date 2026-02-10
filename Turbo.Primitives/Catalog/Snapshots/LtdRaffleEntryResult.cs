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
    public string? BatchId { get; init; }

    [Id(2)]
    public LtdRaffleEntryError? Error { get; init; }

    [Id(3)]
    public CatalogBalanceFailure? BalanceFailure { get; init; }

    public static LtdRaffleEntryResult Succeeded(string batchId) =>
        new()
        {
            Success = true,
            BatchId = batchId,
            Error = null,
            BalanceFailure = null,
        };

    public static LtdRaffleEntryResult Failed(
        LtdRaffleEntryError error,
        CatalogBalanceFailure? balanceFailure = null
    ) =>
        new()
        {
            Success = false,
            BatchId = null,
            Error = error,
            BalanceFailure = balanceFailure,
        };
}
