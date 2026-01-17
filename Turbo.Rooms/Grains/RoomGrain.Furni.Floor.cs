using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> AddFloorItemAsync(IRoomFloorItem item, CancellationToken ct)
    {
        try
        {
            if (!await ActionModule.AddFloorItemAsync(item, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        try
        {
            if (!await ActionModule.PlaceFloorItemAsync(ctx, item, x, y, rot, ct))
                return false;

            return true;
        }
        catch (Exception)
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    )
    {
        try
        {
            if (!await ActionModule.MoveFloorItemByIdAsync(ctx, itemId, x, y, rot, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> RemoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        try
        {
            if (!await ActionModule.RemoveFloorItemByIdAsync(ctx, itemId, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> UseFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        try
        {
            if (!await ActionModule.UseFloorItemByIdAsync(ctx, itemId, ct, param))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> ClickFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        try
        {
            if (!await ActionModule.ClickFloorItemByIdAsync(ctx, itemId, ct, param))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        try
        {
            if (!await ActionModule.ApplyWiredUpdateAsync(ctx, itemId, update, ct))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            // TODO handle exceptions

            return false;
        }
    }

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    ) => FurniModule.GetAllFloorItemSnapshotsAsync(ct);

    public Task<WiredDataSnapshot?> GetWiredDataSnapshotByFloorItemIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.FloorItemsById.TryGetValue(itemId, out var item)
                ? item.Logic is FurnitureWiredLogic wiredLogic
                    ? wiredLogic.GetSnapshot()
                    : null
                : null
        );

    public Task<WiredVariablesSnapshot> GetWiredVariablesSnapshotAsync(CancellationToken ct) =>
        WiredSystem.GetWiredVariablesSnapshotAsync(ct);

    public Task<List<(long id, int value)>> GetAllVariablesForBindingAsync(
        IWiredVariableBinding binding,
        CancellationToken ct
    ) => WiredSystem.GetAllVariablesForBindingAsync(binding, ct);
}
