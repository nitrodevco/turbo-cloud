using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredExecutionContext
{
    public required RoomGrain Room { get; init; }
    public required Dictionary<string, object?> Variables { get; init; }
    public required WiredPolicy Policy { get; init; }
    public required WiredSelectionSet Selected { get; init; }
    public required WiredSelectionSet SelectorPool { get; init; }

    public List<WiredUserMovementSnapshot> UserMoves { get; } = [];
    public List<WiredFloorItemMovementSnapshot> FloorItemMoves { get; } = [];
    public List<WiredWallItemMovementSnapshot> WallItemMoves { get; } = [];
    public List<WiredUserDirectionSnapshot> UserDirections { get; } = [];

    public async Task<WiredSelectionSet> GetWiredSelectionSetAsync(
        FurnitureWiredLogic wired,
        CancellationToken ct
    )
    {
        var set = new WiredSelectionSet();

        foreach (var source in wired.GetFurniSources())
        {
            foreach (var sourceType in source)
            {
                switch (sourceType)
                {
                    case WiredFurniSourceType.SelectedItems:
                        {
                            var stuffIds = wired.WiredData?.StuffIds;

                            if (stuffIds is not null && stuffIds.Count > 0)
                            {
                                foreach (var id in stuffIds)
                                {
                                    if (!Room._liveState.FloorItemsById.ContainsKey(id))
                                        continue;

                                    set.SelectedFurniIds.Add(id);
                                }
                            }
                        }
                        break;
                    case WiredFurniSourceType.TriggeredItem:
                        set.SelectedFurniIds.UnionWith(Selected.SelectedFurniIds);
                        break;
                }
            }
        }

        foreach (var source in wired.GetPlayerSources())
        {
            foreach (var sourceType in source)
            {
                //switch (sourceType) { }
            }
        }

        return set;
    }

    public async Task<WiredSelectionSet> GetEffectiveSelectionAsync(
        FurnitureWiredLogic wired,
        CancellationToken ct
    )
    {
        var result = new WiredSelectionSet();
        var set = await GetWiredSelectionSetAsync(wired, ct);

        foreach (var source in wired.GetFurniSources())
        {
            foreach (var sourceType in source)
            {
                switch (sourceType)
                {
                    case WiredFurniSourceType.SelectedItems:
                        result.SelectedFurniIds.UnionWith(set.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.SelectorItems:
                        result.SelectedFurniIds.UnionWith(SelectorPool.SelectedFurniIds);
                        break;
                    case WiredFurniSourceType.TriggeredItem:
                        result.SelectedFurniIds.UnionWith(Selected.SelectedFurniIds);
                        break;
                }
            }
        }

        foreach (var source in wired.GetPlayerSources())
        {
            foreach (var sourceType in source)
            {
                //switch (sourceType) { }
            }
        }

        return result;
    }

    public void AddFloorItemMovement(IRoomFloorItem floorItem, int tileIdx, Rotation rotation)
    {
        if (floorItem is null)
            return;

        try
        {
            var (sourceX, sourceY, sourceZ) = (floorItem.X, floorItem.Y, floorItem.Z);

            if (!Room._mapModule.MoveFloorItem(floorItem, tileIdx, rotation))
                return;

            FloorItemMoves.Add(
                new()
                {
                    ObjectId = floorItem.ObjectId,
                    SourceX = sourceX,
                    SourceY = sourceY,
                    SourceZ = sourceZ,
                    TargetX = floorItem.X,
                    TargetY = floorItem.Y,
                    TargetZ = floorItem.Z,
                    Rotation = rotation,
                    AnimationTime =
                        Policy.AnimationMode == AnimationModeType.Instant
                            ? 0
                            : Policy.AnimationTimeMs,
                }
            );
        }
        catch { }
    }

    public ActionContext AsActionContext() =>
        new() { Origin = ActionOrigin.Wired, RoomId = Room._roomId };

    public Task SendComposerToRoomAsync(IComposer composer) =>
        Room.SendComposerToRoomAsync(composer);
}
