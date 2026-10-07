using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>What a check of Habbo's gamedata found.</summary>
[GenerateSerializer, Immutable]
public sealed record HabboCheckResult
{
    /// <summary>The release Habbo serves now.</summary>
    [Id(0)]
    public required HabboReleaseSnapshot Release { get; init; }

    /// <summary>Whether it is one the hotel had not seen before this check.</summary>
    [Id(1)]
    public required bool IsNew { get; init; }

    /// <summary>The version of its external texts Habbo serves now.</summary>
    [Id(2)]
    public required HabboTextVersionSnapshot Texts { get; init; }

    [Id(3)]
    public required bool TextsAreNew { get; init; }

    /// <summary>The version of its product data Habbo serves now.</summary>
    [Id(4)]
    public required HabboProductVersionSnapshot Products { get; init; }

    [Id(5)]
    public required bool ProductsAreNew { get; init; }

    /// <summary>The version of its figure data Habbo serves now.</summary>
    [Id(6)]
    public required HabboFigureVersionSnapshot Figures { get; init; }

    [Id(7)]
    public required bool FiguresAreNew { get; init; }
}
