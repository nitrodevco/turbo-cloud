using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Primitives.Sound;

/// <summary>
/// What makes a furni a song disk and which song is on it. A disk is a furni of the trax song
/// category, which is how the client tells one too; its song id is its legacy stuff data, put
/// there when it is bought (<see cref="ProductStuffData"/>). The client never reads that string:
/// it reads the song id from the number the server writes beside the item, both in the inventory
/// (<c>FurnitureItemSnapshot.Extra</c>) and in the room (the floor object's extras), which a disk
/// standing in a room gets from its logic, <see cref="LOGIC"/>.
/// </summary>
public static class SongDisks
{
    public const string LOGIC = "song_disk";

    /// <summary>No song: what a disk whose stuff data names none carries. Song ids start at one.</summary>
    public const int NO_SONG = 0;

    public static bool IsSongDisk(FurnitureDefinitionSnapshot definition) =>
        definition.FurniCategory == FurnitureCategory.TraxSong;

    /// <summary>The song on a disk, or <see cref="NO_SONG"/> when its stuff data names none.</summary>
    public static int SongIdOf(IStuffData stuffData) =>
        ProductStuffData.TryParseSongId(stuffData.GetLegacyString(), out var songId)
            ? songId
            : NO_SONG;
}
