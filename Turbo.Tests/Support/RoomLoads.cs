using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Tests.Support;

/// <summary>
/// One room over several loads. The room's furni rows live here; <see cref="LoadAsync"/>
/// activates a new room grain (a <see cref="WiredRoom"/>) that reads its furni from them through
/// the items provider, as <c>RoomFurniModule.EnsureFurniLoadedAsync</c> does in the hotel, and
/// <see cref="UnloadAsync"/> deactivates it as the silo does an empty room
/// (<c>RoomGrain.OnDeactivateAsync</c>), writing back what it hands to persistence. Whatever the
/// room kept only in memory is gone in the next load. Every load runs on the one
/// <see cref="Clock"/>.
/// </summary>
public sealed class RoomLoads(int width = 8, int height = 8, WiredConfig? wiredConfig = null)
{
    private sealed record Row(int Id, int X, int Y, string LogicName, int TotalStates)
    {
        public string? ExtraData { get; set; }
    }

    private readonly Dictionary<int, Row> _rows = [];

    public ManualTimeProvider Clock { get; } = new(ManualTimeProvider.FixedStart);

    /// <summary>The loaded room, or null while it is unloaded.</summary>
    public WiredRoom? Room { get; private set; }

    /// <summary>A floor furni row: in the room from its next load.</summary>
    public void AddFloorItem(int id, int x, int y, string logicName, int totalStates = 2) =>
        _rows[id] = new Row(id, x, y, logicName, totalStates);

    public async Task<WiredRoom> LoadAsync(CancellationToken ct)
    {
        var room = Activate();

        await room.Harness.Module<RoomFurniModule>().EnsureFurniLoadedAsync(ct);

        return room;
    }

    /// <summary>
    /// The room's grain as a call from another room activates it, nobody on the way in: up, but
    /// its furni not read yet (only <c>RoomGrain.EnsureRoomActiveAsync</c> reads them).
    /// </summary>
    public WiredRoom Activate()
    {
        if (Room is not null)
            throw new InvalidOperationException("The room is loaded already.");

        var room = new WiredRoom(width, height, clock: Clock);

        room.Harness.State.IsFurniLoaded = false;

        if (wiredConfig is not null)
            room.Harness.Room.UseWiredConfig(wiredConfig);

        room.Harness.Fakes.Handlers["LoadByRoomIdAsync"] = _ =>
            Task.FromResult<(
                IReadOnlyList<IRoomFloorItem>,
                IReadOnlyList<IRoomWallItem>,
                IReadOnlyDictionary<PlayerId, string>
            )>(
                (
                    [
                        .. _rows.Values.Select(row =>
                            WiredRoom.NewFloorItem(
                                row.Id,
                                row.X,
                                row.Y,
                                row.LogicName,
                                totalStates: row.TotalStates,
                                extraData: row.ExtraData
                            )
                        ),
                    ],
                    [],
                    new Dictionary<PlayerId, string>()
                )
            );
        room.Harness.Fakes.Handlers["EnqueueDirtyItemsAsync"] = call =>
        {
            foreach (var snapshot in (IEnumerable<RoomItemSnapshot>)call.Args[1]!)
            {
                if (_rows.TryGetValue(snapshot.ObjectId.Value, out var row))
                    row.ExtraData = snapshot.ExtraData;
            }

            return Task.CompletedTask;
        };

        return Room = room;
    }

    /// <summary>Everyone has left: the room is deactivated and its changes written.</summary>
    public async Task UnloadAsync(CancellationToken ct)
    {
        var room = Room ?? throw new InvalidOperationException("The room is not loaded.");

        if (room.Harness.State.AvatarsByObjectId.Count > 0)
            throw new InvalidOperationException("An empty room is unloaded, not this one.");

        await ((Grain)room.Harness.Room).OnDeactivateAsync(
            new(DeactivationReasonCode.None, ""),
            ct
        );

        Room = null;
    }
}
