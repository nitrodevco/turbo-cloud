using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>What importing a habbo-free-flow-chat-com bundle did.</summary>
[GenerateSerializer, Immutable]
public sealed record ChatStyleImportResult
{
    [Id(0)]
    public required int Added { get; init; }

    [Id(1)]
    public required int Updated { get; init; }

    /// <summary>The library's other bitmaps kept.</summary>
    [Id(2)]
    public required int LibraryBitmaps { get; init; }

    /// <summary>Styles the hotel changed since it took them from Habbo's client, left as the hotel has them.</summary>
    [Id(3)]
    public int Kept { get; init; }
}
