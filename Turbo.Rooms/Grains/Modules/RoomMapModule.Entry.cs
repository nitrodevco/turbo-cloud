using System;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule
{
    private RoomModelSnapshot? _entryTileModel = null;
    private (int X, int Y) _entryTile = (0, 0);

    /// <summary>
    /// The column of the tile people come in on, and the one the client is told is the door. See
    /// <see cref="GetEntryTile"/>.
    /// </summary>
    public int EntryX => GetEntryTile().X;

    /// <summary>The row of the tile people come in on. See <see cref="GetEntryTile"/>.</summary>
    public int EntryY => GetEntryTile().Y;

    /// <summary>
    /// The model's door when it is a tile of the model, else the open tile nearest it. A model
    /// row written by hand or brought in by the import can put its door off the map or on a hole,
    /// which the floor plan editor refuses: avatars then came in on the hole, or on 0,0 for a door
    /// off the map, whatever was there. Worked out once per model, so the warning is logged once.
    /// </summary>
    private (int X, int Y) GetEntryTile()
    {
        var model = State.Model;

        if (!ReferenceEquals(model, _entryTileModel))
        {
            _entryTile = FindEntryTile(model);
            _entryTileModel = model;
        }

        return _entryTile;
    }

    private (int X, int Y) FindEntryTile(RoomModelSnapshot? model)
    {
        if (model is null)
            return (0, 0);

        var (doorX, doorY) = (model.DoorX, model.DoorY);

        if (IsOpenInModel(model, doorX, doorY))
            return (doorX, doorY);

        var nearest = -1;
        var nearestDistance = long.MaxValue;

        for (var idx = 0; idx < model.BaseFlags.Length; idx++)
        {
            if (!model.BaseFlags[idx].Has(RoomTileFlags.Open))
                continue;

            long dx = (idx % model.Width) - doorX;
            long dy = (idx / model.Width) - doorY;
            var distance = (dx * dx) + (dy * dy);

            if (distance < nearestDistance)
            {
                nearest = idx;
                nearestDistance = distance;
            }
        }

        if (nearest < 0)
        {
            // Nothing to stand on anywhere: the door is kept, brought onto the map so that what
            // reads its tile still reads one.
            var keptX = Math.Clamp(doorX, 0, Math.Max(model.Width - 1, 0));
            var keptY = Math.Clamp(doorY, 0, Math.Max(model.Height - 1, 0));

            Logger.LogWarning(
                "Room {RoomId}'s model {ModelId} has no open tile; people come in at ({EntryX}, {EntryY}), on nothing",
                _roomGrain.RoomId,
                model.Id,
                keptX,
                keptY
            );

            return (keptX, keptY);
        }

        var entry = (X: nearest % model.Width, Y: nearest / model.Width);

        Logger.LogWarning(
            "Room {RoomId}'s model {ModelId} has its door at ({DoorX}, {DoorY}), which is no tile of it; people come in at ({EntryX}, {EntryY})",
            _roomGrain.RoomId,
            model.Id,
            doorX,
            doorY,
            entry.X,
            entry.Y
        );

        return entry;
    }

    private static bool IsOpenInModel(RoomModelSnapshot model, int x, int y) =>
        (uint)x < (uint)model.Width
        && (uint)y < (uint)model.Height
        && model.BaseFlags[(y * model.Width) + x].Has(RoomTileFlags.Open);
}
