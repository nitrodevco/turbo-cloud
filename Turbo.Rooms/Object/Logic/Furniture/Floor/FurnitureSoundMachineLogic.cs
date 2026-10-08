using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A trax machine (sound machine). This client only plays one: switched on, it asks for the
/// machine's songs and how far into them the room is (<c>GetSoundMachinePlayList</c>) and plays
/// the list round by itself. It has no editor, so nothing can compose a song or put a disk into
/// a machine; the playlist and the clock are the jukebox's, and a machine holds the disks its
/// grain has (none, unless put there by other means).
/// </summary>
[RoomObjectLogic("sound_machine")]
public class FurnitureSoundMachineLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureJukeboxLogic(stuffDataFactory, ctx);
