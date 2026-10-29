using System.Collections.Immutable;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A proposed repair; null After means the saved data needs manual correction.</summary>
public sealed record VendingRepairItemSnapshot
{
    public required int Id { get; init; }
    public required string ClassName { get; init; }
    public required string Version { get; init; }
    public required string Before { get; init; }
    public string? After { get; init; }
    public string? Reason { get; init; }
    public required ImmutableArray<int> HandItems { get; init; }
    public required bool Animates { get; init; }
    public required int CurrentUsagePolicy { get; init; }
    public required int DefaultUsagePolicy { get; init; }
}
