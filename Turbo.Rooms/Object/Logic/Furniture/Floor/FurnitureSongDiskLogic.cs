using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Sound;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A song disk standing in a room. The client shows its song in the info stand, reading the song
/// id from the floor object's extras rather than from the stuff data where it is kept
/// (<see cref="SongDisks"/>).
/// </summary>
[RoomObjectLogic(SongDisks.LOGIC)]
public class FurnitureSongDiskLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override int GetObjectExtra() => SongDisks.SongIdOf(StuffData);
}
