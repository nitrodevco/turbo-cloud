using System.Collections.Immutable;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A page of known vending definitions with missing or unreadable configuration.</summary>
public sealed record VendingRepairPreviewSnapshot
{
    public required ImmutableArray<VendingRepairItemSnapshot> Items { get; init; }
    public required int Total { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
