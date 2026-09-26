using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// The floor plan a room was built on, and saving a new one over it. A model row is shared by
/// every room built on it, so a room that draws its own gets a row of its own, marked custom so
/// it is never offered to somebody creating a room; the second save edits that row rather than
/// leaving another behind.
/// </summary>
public sealed partial class RoomMapModule
{
    /// <summary>The characters a tile height may be written as, lowest first.</summary>
    private const string HEIGHT_CHARACTERS = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>A tile that is not part of the room at all.</summary>
    private const char CLOSED_TILE = 'x';

    /// <summary>
    /// Saves a floor plan over this room's. Returns false for anything the room will not take,
    /// which is logged with the reason: the editor has already checked all of it, so a refusal
    /// means the request did not come from the editor.
    /// </summary>
    public async Task<bool> SaveFloorPlanAsync(
        string modelData,
        FloorPlanPropertiesSnapshot? properties,
        CancellationToken ct
    )
    {
        if (!TryReadFloorPlan(modelData, out var rows, out var reason))
        {
            _roomGrain._logger.LogWarning(
                "Room {RoomId} refused a floor plan: {Reason}",
                _roomGrain.RoomId,
                reason
            );

            return false;
        }

        var width = rows.Max(x => x.Length);
        var height = rows.Count;
        var model = _roomGrain._state.Model;

        // Keeping the door where it was is the answer both to a save that does not mention one
        // and to one whose door the new plan has no tile for.
        var doorX = model?.DoorX ?? 0;
        var doorY = model?.DoorY ?? 0;
        var doorRotation = model?.DoorRotation ?? Rotation.North;

        if (properties is not null && IsDoorOnAnOpenTile(rows, properties.DoorX, properties.DoorY))
        {
            doorX = properties.DoorX;
            doorY = properties.DoorY;
            doorRotation = ReadRotation(properties.DoorRotation, doorRotation);
        }

        if (!IsDoorOnAnOpenTile(rows, doorX, doorY))
        {
            reason = "it has no tile for the door, old or new";

            _roomGrain._logger.LogWarning(
                "Room {RoomId} refused a floor plan: {Reason}",
                _roomGrain.RoomId,
                reason
            );

            return false;
        }

        var cleaned = string.Join('\r', rows);

        var modelId = await WriteModelAsync(cleaned, doorX, doorY, doorRotation, ct);

        if (modelId is not { } savedModelId)
            return false;

        var reloaded = await _roomGrain._roomModelProvider.ReloadModelAsync(savedModelId, ct);

        if (reloaded is null)
            return false;

        _roomGrain._logger.LogInformation(
            "Room {RoomId} saved a floor plan of {Width}x{Height} on model {ModelId}",
            _roomGrain.RoomId,
            width,
            height,
            savedModelId
        );

        await RebuildForModelAsync(reloaded, ct);

        if (properties is not null)
            await _roomGrain.ApplyFloorPlanSettingsAsync(properties, ct);

        return true;
    }

    private static Rotation ReadRotation(int value, Rotation fallback) =>
        value >= 0 && Enum.IsDefined((Rotation)value) ? (Rotation)value : fallback;

    /// <summary>
    /// Reads the plan the client drew into its rows, or says why it will not do. Everything here
    /// is a limit the editor keeps to as well, so none of it should ever be hit by a real save.
    /// </summary>
    private bool TryReadFloorPlan(string modelData, out List<string> rows, out string reason)
    {
        rows = [];
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(modelData))
        {
            reason = "it is empty";

            return false;
        }

        rows =
        [
            .. modelData
                .Replace("\r\n", "\r")
                .Replace("\n", "\r")
                .ToLowerInvariant()
                .Split('\r')
                .Select(x => x.TrimEnd()),
        ];

        // A trailing newline is how the editor ends the last row, not a row of nothing.
        while (rows.Count > 0 && rows[^1].Length == 0)
            rows.RemoveAt(rows.Count - 1);

        if (rows.Count == 0)
        {
            reason = "it has no rows";

            return false;
        }

        var width = rows.Max(x => x.Length);
        var height = rows.Count;
        var maxAxis = _roomGrain._roomConfig.FloorPlanMaxAxisLength;

        if (width > maxAxis || height > maxAxis)
        {
            reason = $"it is {width}x{height} and no axis may pass {maxAxis}";

            return false;
        }

        // Counted the way the editor counts it, so the two agree on what is too big.
        if ((width - 1) * (height - 1) > _roomGrain._roomConfig.FloorPlanMaxArea)
        {
            reason = $"it covers more than {_roomGrain._roomConfig.FloorPlanMaxArea} tiles";

            return false;
        }

        foreach (var row in rows)
        {
            foreach (var tile in row)
            {
                if (tile != CLOSED_TILE && !HEIGHT_CHARACTERS.Contains(tile))
                {
                    reason = $"'{tile}' is not a tile height";

                    return false;
                }
            }
        }

        if (!rows.Any(row => row.Any(tile => tile != CLOSED_TILE)))
        {
            reason = "every tile is closed";

            return false;
        }

        return true;
    }

    private static bool IsDoorOnAnOpenTile(List<string> rows, int doorX, int doorY)
    {
        if (doorY < 0 || doorY >= rows.Count)
            return false;

        var row = rows[doorY];

        return doorX >= 0 && doorX < row.Length && row[doorX] != CLOSED_TILE;
    }

    /// <summary>
    /// Writes the plan to the room's own model row, making one the first time. Null when the
    /// write failed, in which case the room is left on the model it had.
    /// </summary>
    private async Task<int?> WriteModelAsync(
        string modelData,
        int doorX,
        int doorY,
        Rotation doorRotation,
        CancellationToken ct
    )
    {
        try
        {
            await using var dbCtx = await _roomGrain._dbCtxFactory.CreateDbContextAsync(ct);

            var room = await dbCtx.Rooms.FirstOrDefaultAsync(
                x => x.Id == _roomGrain.RoomId.Value,
                ct
            );

            if (room is null)
                return null;

            var model = await dbCtx.RoomModels.FirstOrDefaultAsync(
                x => x.Id == room.RoomModelEntityId,
                ct
            );

            // A model the room does not own is shared with every other room built on it, so the
            // first save copies it rather than writing through.
            if (model is null || !model.Custom || model.Name != CustomModelName)
            {
                model = new RoomModelEntity
                {
                    Name = CustomModelName,
                    Model = modelData,
                    DoorX = doorX,
                    DoorY = doorY,
                    DoorRotation = doorRotation,
                    Enabled = true,
                    Custom = true,
                };

                dbCtx.RoomModels.Add(model);

                await dbCtx.SaveChangesAsync(ct);

                room.RoomModelEntityId = model.Id;
            }
            else
            {
                model.Model = modelData;
                model.DoorX = doorX;
                model.DoorY = doorY;
                model.DoorRotation = doorRotation;
            }

            await dbCtx.SaveChangesAsync(ct);

            return model.Id;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to save the floor plan of room {RoomId}",
                _roomGrain.RoomId
            );

            return null;
        }
    }

    /// <summary>The name of the model row a room draws its own plan into; one per room.</summary>
    private string CustomModelName => $"room_{_roomGrain.RoomId.Value}";

    /// <summary>
    /// Puts the room back together on a new plan. Everything standing on the old tiles has to
    /// let go of them first, because the arrays are about to be a different size: furni the new
    /// plan has no tile for goes home to its owner, and every avatar is put at the door, which
    /// the new plan is guaranteed to have.
    /// </summary>
    private async Task RebuildForModelAsync(RoomModelSnapshot model, CancellationToken ct)
    {
        // Sent home while the old map still stands, so they leave the room the ordinary way:
        // once the arrays are swapped there is no tile left to take them off.
        var homeless = _roomGrain
            ._state.ItemsById.Values.Where(x => !FitsOnModel(model, x))
            .ToList();

        if (homeless.Count > 0)
        {
            _roomGrain._logger.LogInformation(
                "The new floor plan of room {RoomId} has no tile for {Count} furni, which went home",
                _roomGrain.RoomId,
                homeless.Count
            );

            await _roomGrain.ActionModule.ReturnItemsToOwnersAsync(homeless, ct);
        }

        var avatars = _roomGrain._state.AvatarsByObjectId.Values.ToList();

        foreach (var avatar in avatars)
            RemoveAvatar(avatar, false);

        var items = _roomGrain._state.ItemsById.Values.ToList();

        foreach (var item in items)
            RemoveItem(item);

        _roomGrain._state.Model = model;
        _roomGrain._state.IsMapReady = false;

        await EnsureMapBuiltAsync(ct);

        _roomGrain._state.IsTileComputationPaused = true;

        foreach (var item in items)
            AddItem(item);

        var doorIdx = ToIdx(model.DoorX, model.DoorY);

        foreach (var avatar in avatars)
        {
            avatar.SetPosition(model.DoorX, model.DoorY);
            avatar.NextTileId = -1;

            AddAvatar(avatar, false);
        }

        _roomGrain._state.IsTileComputationPaused = false;

        ComputeAllTiles();
        _roomGrain._state.DirtyHeightTileIds.Clear();

        foreach (var avatar in avatars)
        {
            avatar.SetPositionZ(GetTileHeightForAvatar(doorIdx));
            avatar.NeedsInvoke = true;
        }

        // The cached map snapshot describes a room that no longer exists.
        _dirty = true;
    }

    /// <summary>
    /// Whether a plan has an open tile under every tile this furni covers. A floor item is
    /// measured over its whole footprint, turned the way it stands; a wall item hangs off the
    /// one tile it is pinned to.
    /// </summary>
    private static bool FitsOnModel(RoomModelSnapshot model, IRoomItem item)
    {
        if (item is not IRoomFloorItem floor)
            return IsOpenOnModel(model, item.X, item.Y);

        return FloorFootprint.Of(floor).Tiles().All(tile => IsOpenOnModel(model, tile.X, tile.Y));
    }

    private static bool IsOpenOnModel(RoomModelSnapshot model, int x, int y) =>
        (uint)x < (uint)model.Width
        && (uint)y < (uint)model.Height
        && !model.BaseFlags[(y * model.Width) + x].Has(RoomTileFlags.Disabled);
}
