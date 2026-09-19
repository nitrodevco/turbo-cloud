using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

/// <summary>
/// How a variable fx looks: the presentation half of a variable fx addon, as the client's
/// <c>VariableFxConfigUpdateData</c> reads it. It names no variable and no entity; those arrive
/// with each <see cref="VariableFxStatusSnapshot"/>, which points back here by
/// <see cref="ConfigId"/> (the addon's furni id).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record VariableFxConfigSnapshot
{
    [Id(0)]
    public required int ConfigId { get; init; }

    /// <summary>Drawn over avatars when true, over furni when false.</summary>
    [Id(1)]
    public required bool IsUserFx { get; init; }

    [Id(2)]
    public required VariableFxShowModeType ShowMode { get; init; }

    /// <summary>
    /// Which kinds of change show the fx again: one bit per checkbox of the editor's update
    /// mask group, which the client applies itself. Only localisation keys name them.
    /// </summary>
    [Id(3)]
    public required int UpdateMask { get; init; }

    [Id(4)]
    public required bool ShowOnMouseHover { get; init; }

    [Id(5)]
    public required int ShowDurationMs { get; init; }

    [Id(6)]
    public required VariableFxCategoryType Category { get; init; }

    [Id(7)]
    public required int StyleId { get; init; }

    [Id(8)]
    public required int ColorId { get; init; }

    [Id(9)]
    public required int WidthId { get; init; }

    [Id(10)]
    public required int RendererId { get; init; }

    [Id(11)]
    public required long DefaultMinValue { get; init; }

    [Id(12)]
    public required long DefaultMaxValue { get; init; }

    /// <summary>Renderer options by the client's own key names (segments, sub_renderer, icon, icon_alignment).</summary>
    [Id(13)]
    public required ImmutableDictionary<string, string> Extra { get; init; }
}
