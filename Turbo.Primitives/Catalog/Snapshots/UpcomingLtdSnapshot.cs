using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>
/// Data required for the Landing View LTD countdown.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record UpcomingLtdSnapshot
{
    [Id(0)]
    public required int SecondsUntil { get; init; }

    [Id(1)]
    public required int PageId { get; init; }

    [Id(2)]
    public required int OfferId { get; init; }

    [Id(3)]
    public required string? ClassName { get; init; }
}
