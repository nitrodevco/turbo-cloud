namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>An explicitly selected preview version, optionally accepting its known usage policy.</summary>
public sealed record VendingRepairSelectionSnapshot
{
    public required int Id { get; init; }
    public required string Version { get; init; }
    public bool UseDefaultUsagePolicy { get; init; }
}
