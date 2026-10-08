namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Sort order of the "filter by variable" addons, as the editor lists them
/// (<c>wiredfurni.params.variables.sort_by.0</c> to <c>.5</c>).
/// </summary>
public enum WiredVariableSortType
{
    HighestValue = 0,
    LowestValue = 1,
    CreationOldestFirst = 2,
    CreationNewestFirst = 3,
    UpdateOldestFirst = 4,
    UpdateNewestFirst = 5,
}
