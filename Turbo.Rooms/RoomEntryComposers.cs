using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms;

/// <summary>
/// The room itself as a player walking in is shown it, from the view the room took in one turn:
/// what a room entry (<c>RoomService.EnterRoomAsync</c>) and a watch's first view
/// (<c>RoomGrain.GetWatchViewAsync</c>) both send.
/// </summary>
internal static class RoomEntryComposers
{
    /// <summary>
    /// In Habbo's order (hhus, four entries on two builds, 2026-10-13): RoomReady,
    /// RoomEntryTile, the room's properties, the maps, the visualization, Users, the kept WIRED
    /// visualization states, Objects, Items. The room's chat settings are not part of it: the
    /// client takes them from GetGuestRoomResult.
    /// </summary>
    public static IEnumerable<IComposer> RoomOf(
        RoomId roomId,
        RoomEntryViewSnapshot view,
        RoomConfig roomConfig
    )
    {
        var snapshot = view.Room;
        var mapSnapshot = view.Map;

        // The client's initial camera target is the door tile. Its height is read back the way the
        // client reads the stacking height map: -1 is no tile, the bits below the stacking-blocked
        // bit are the height x 256 (a blocked tile is negative under Habbo's bit 15).
        var doorTileIndex = mapSnapshot.DoorY * mapSnapshot.Width + mapSnapshot.DoorX;
        var doorEncoded =
            doorTileIndex >= 0 && doorTileIndex < mapSnapshot.TileEncodedHeights.Length
                ? mapSnapshot.TileEncodedHeights[doorTileIndex]
                : (short)-1;
        var doorAltitude =
            doorEncoded == -1
                ? Altitude.Zero
                : Altitude.FromValue(
                    (doorEncoded & ((1 << roomConfig.StackingBlockedMaskBit) - 1)) / 256.0
                );

        yield return new RoomReadyMessageComposer
        {
            WorldType = snapshot.WorldType,
            RoomId = roomId,
        };
        yield return new RoomEntryTileMessageComposer
        {
            X = mapSnapshot.DoorX,
            Y = mapSnapshot.DoorY,
            Rotation = mapSnapshot.DoorRotation,
        };

        foreach (var property in view.Properties)
            yield return new RoomPropertyMessageComposer
            {
                Key = RoomPropertyTypeExtensions.GetString(property.Key),
                Value = property.Value,
            };

        yield return new HeightMapMessageComposer
        {
            Width = mapSnapshot.Width,
            Size = mapSnapshot.Size,
            Heights = mapSnapshot.TileEncodedHeights,
        };
        yield return new FloorHeightMapMessageComposer
        {
            ScaleType = roomConfig.DefaultRoomScale,
            // The room's own choice, made in the floor plan editor; a room that has
            // never been drawn keeps the hotel's default.
            FixedWallsHeight =
                snapshot.WallHeight >= 0 ? snapshot.WallHeight : roomConfig.DefaultWallHeight,
            ModelData = mapSnapshot.ModelData,
            AreaHideData = [.. view.AreaHides],
            CameraInitX = mapSnapshot.DoorX,
            CameraInitY = mapSnapshot.DoorY,
            CameraInitZ = doorAltitude,
        };
        yield return new RoomVisualizationSettingsMessageComposer
        {
            WallsHidden = snapshot.HideWalls,
            WallThickness = snapshot.WallThickness,
            FloorThickness = snapshot.FloorThickness,
        };
        yield return new UsersMessageComposer { Avatars = view.Avatars };

        // Right after the first Users, before Objects; not sent when empty (official client
        // capture 2026-10-20, 3 of 3 entries each way).
        foreach (var state in view.WiredVisualization)
            yield return state;

        yield return new ObjectsMessageComposer
        {
            OwnerNames = view.OwnerNames,
            FloorItems = view.FloorItems,
        };
        yield return new ItemsMessageComposer
        {
            OwnerNames = view.OwnerNames,
            WallItems = view.WallItems,
        };
    }
}
