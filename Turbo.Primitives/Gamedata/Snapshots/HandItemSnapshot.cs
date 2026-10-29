namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A server hand-item id and the figure part its carry action draws.</summary>
public sealed record HandItemSnapshot
{
    public required int Id { get; init; }
    public string? Name { get; init; }
    public required int AssetPartId { get; init; }
    public string? Library { get; init; }
    public required bool AssetAvailable { get; init; }
    public required bool PreviewAvailable { get; init; }
}
