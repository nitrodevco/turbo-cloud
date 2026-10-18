namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// A way to read the room's tiles. The map is the room as it stands; a caller weighing moves it
/// has not made yet answers with those moves applied, through the same
/// <see cref="RoomMapModule.EvaluateTile"/>, so a placement is judged by one set of rules either
/// way.
/// </summary>
public interface IRoomTileView
{
    /// <summary>The tile at an index inside the map.</summary>
    public RoomTileState GetTile(int tileIdx);
}
