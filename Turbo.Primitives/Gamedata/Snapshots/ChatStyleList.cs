using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>Every chat style in the client's order, the id a new one takes, and the bundle built from them.</summary>
[GenerateSerializer, Immutable]
public sealed record ChatStyleList
{
    [Id(0)]
    public required ImmutableArray<ChatStyleDefinitionSnapshot> Styles { get; init; }

    /// <summary>The lowest client id under 1000 no style has.</summary>
    [Id(1)]
    public required int NextId { get; init; }

    /// <summary>The habbo-free-flow-chat-com bundle as built last; null before it was.</summary>
    [Id(2)]
    public AssetBundleSnapshot? Bundle { get; init; }

    /// <summary>The library's bitmaps that are no style's, kept from the bundle imported last.</summary>
    [Id(3)]
    public required int LibraryBitmaps { get; init; }
}
