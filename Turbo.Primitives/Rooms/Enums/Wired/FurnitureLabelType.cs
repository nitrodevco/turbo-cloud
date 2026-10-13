namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// What a <c>FurnitureLabelsAndRegions</c> entry describes (Flash <c>FurnitureLabelData</c> type
/// constants, read first by the entry parser <c>_-MK.parse</c>). An entry of any other type ends
/// the client's parse.
/// </summary>
public enum FurnitureLabelType
{
    /// <summary>A label: text in a bubble over the furni, with a colour, an audience and "always open".</summary>
    Label = 0,

    /// <summary>A region: a title, a description, an offset, a minimum size, a colour and areas.</summary>
    Region = 1,
}
