using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a WIRED label furni (<c>wf_label</c>) says and how it shows it, under <see cref="SECTION"/>
/// in the item extra data. An item that was never written to reads the defaults, which are the
/// ones Flash gives a label built without them (<c>FurnitureLabelData</c> constructor:
/// <c>DEFAULT_COLOR</c> 4278912 = #414A80, visibility 1, not always open).
/// </summary>
public sealed record FurnitureLabelData
{
    public const string SECTION = "furniture_label";

    /// <summary>Flash <c>FurnitureLabelData.DEFAULT_COLOR</c>, the settings window's "Blue".</summary>
    public const int DEFAULT_COLOR = 4278912;

    public string Text { get; init; } = string.Empty;

    public int BubbleColor { get; init; } = DEFAULT_COLOR;

    public FurnitureLabelVisibilityType Visibility { get; init; } =
        FurnitureLabelVisibilityType.WiredInspectors;

    public bool AlwaysOpen { get; init; }
}
