using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Contracts.Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Events.Rooms;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Furniture;
using Turbo.Primitives.Orleans.Snapshots.Room.Mapping;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Orleans.States.Room;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms.Grains;

public class RoomGrain(
    [PersistentState(OrleansStateNames.ROOM_STATE, OrleansStorageNames.ROOM_STORE)]
        IPersistentState<RoomState> state,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IOptions<RoomConfig> roomConfig,
    IRoomModelProvider roomModelProvider,
    IRoomItemsLoader itemsLoader,
    IGrainFactory grainFactory
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly IRoomItemsLoader _itemsLoader = itemsLoader;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(10);

    private RoomModelSnapshot? _roomModel = null;
    private RoomMapState? _roomMapState = null;
    private RoomMapSnapshot? _roomMapSnapshot = null;
    private readonly Dictionary<long, IRoomFloorItem> _floorItemsById = [];
    private readonly List<int> _pendingTileIdxUpdates = [];

    private int _mapVersion = 0;

    private IAsyncStream<RoomEvent>? _stream = null;
    private IDisposable? _updateTimer;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

        _stream = provider.GetStream<RoomEvent>(
            StreamId.Create(OrleansStreamNames.ROOM_EVENTS, this.GetPrimaryKeyLong())
        );

        await HydrateFromExternalAsync(ct);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .UpsertActiveRoomAsync(state.State.RoomSnapshot);

        _updateTimer = this.RegisterGrainTimer<object?>(
            async _ => await FlushUpdatesAsync(CancellationToken.None),
            null,
            _updateInterval,
            _updateInterval
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _updateTimer?.Dispose();

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .RemoveActiveRoomAsync(this.GetPrimaryKeyLong());

        await WriteToDatabaseAsync(ct);
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        if (state.State.IsLoaded)
            return;

        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            var entity =
                await dbCtx
                    .Rooms.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
                ?? throw new Exception(
                    $"RoomGrain:{this.GetPrimaryKeyLong()} not found in database."
                );

            state.State.RoomSnapshot = new RoomSnapshot
            {
                RoomId = entity.Id,
                Name = entity.Name ?? string.Empty,
                Description = entity.Description ?? string.Empty,
                OwnerId = (long)entity.PlayerEntityId,
                OwnerName = string.Empty,
                Population = 0,
                DoorMode = entity.DoorMode,
                PlayersMax = entity.PlayersMax,
                TradeType = entity.TradeType,
                Score = 0,
                Ranking = 0,
                CategoryId = entity.NavigatorCategoryEntityId ?? -1,
                Tags = [],
                AllowPets = entity.AllowPets,
                AllowPetsEat = entity.AllowPetsEat,
                Password = entity.Password ?? string.Empty,
                ModSettings = new ModSettingsSnapshot
                {
                    WhoCanMute = entity.MuteType,
                    WhoCanKick = entity.KickType,
                    WhoCanBan = entity.BanType,
                },
                ChatSettings = new ChatSettingsSnapshot
                {
                    ChatMode = entity.ChatModeType,
                    BubbleWidth = entity.ChatBubbleType,
                    ScrollSpeed = entity.ChatSpeedType,
                    FullHearRange = entity.ChatDistance,
                    FloodSensitivity = entity.ChatFloodType,
                },
                LastUpdatedUtc = DateTime.UtcNow,
            };

            state.State.ModelId = entity.RoomModelEntityId;
            state.State.IsLoaded = true;

            await state.WriteStateAsync(ct);
        }
        catch (Exception e)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            /* await dbCtx
                .Rooms.Where(r => r.Id == this.GetPrimaryKeyLong())
                .ExecuteUpdateAsync(up => { }, ct); */
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }

    public async Task EnsureMapBuiltAsync(CancellationToken ct)
    {
        _roomModel ??=
            _roomModelProvider.GetModelById(state.State.ModelId)
            ?? throw new Exception("Invalid room model");

        if (_roomMapState is null)
        {
            var tileHighestFloorItems = new long[_roomModel.Size];
            var tileFloorStacks = new List<long>[_roomModel.Size];

            for (int idx = 0; idx < _roomModel.Size; idx++)
            {
                tileHighestFloorItems[idx] = -1;
                tileFloorStacks[idx] = [];
            }

            _roomMapState = new RoomMapState
            {
                TileHeights = new double[_roomModel.Size],
                TileRelativeHeights = new short[_roomModel.Size],
                TileStates = new byte[_roomModel.Size],
                TileHighestFloorItems = tileHighestFloorItems,
                TileFloorStacks = tileFloorStacks,
            };

            var floorItems = await _itemsLoader.LoadByRoomIdAsync(this.GetPrimaryKeyLong(), ct);

            foreach (var item in floorItems)
                await AddFloorItemAsync(item, ct, true);

            ComputeMap();
        }

        if (_roomMapSnapshot is null || _mapVersion != _roomMapSnapshot.Version)
            await BuildMapSnapshotAsync(ct);
    }

    public async Task<bool> AddFloorItemAsync(
        IRoomFloorItem item,
        CancellationToken ct,
        bool skipCompute = false
    )
    {
        if (_roomMapState is null)
            throw new Exception("Room map state is not initialized.");

        if (!_floorItemsById.TryAdd(item.Id, item))
            return false;

        if (GetTileIdxForFloorItem(item, out var tileIdxs))
        {
            foreach (var idx in tileIdxs)
            {
                _roomMapState.TileFloorStacks[idx].Add(item.Id);

                if (!skipCompute)
                    ComputeTile(idx);
            }
        }

        _mapVersion++;

        return true;
    }

    public async Task MoveFloorItemByIdAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        if (_roomMapState is null)
            throw new Exception("Room map state is not initialized.");

        if (!_floorItemsById.TryGetValue(itemId, out var item))
            throw new KeyNotFoundException($"Floor item not found: ItemId={itemId}");

        if (item.X == newX && item.Y == newY && item.Rotation == newRotation)
            return;

        if (GetTileIdxForFloorItem(item, out var oldTileIdxs))
        {
            foreach (var idx in oldTileIdxs)
            {
                _roomMapState.TileFloorStacks[idx].Remove(item.Id);

                ComputeTile(idx);
            }
        }

        var newIdx = Idx(newX, newY);
        var newZ = _roomMapState.TileHeights[newIdx];

        item.SetPosition(newX, newY, newZ);
        item.SetRotation(newRotation);

        if (GetTileIdxForFloorItem(item, out var newTileIdxs))
        {
            foreach (var idx in newTileIdxs)
            {
                _roomMapState.TileFloorStacks[idx].Add(item.Id);

                ComputeTile(idx);
            }
        }

        _mapVersion++;

        await SendComposerToRoomAsync(
            new ObjectUpdateMessageComposer
            {
                FloorItem = RoomFloorItemSnapshot.FromFloorItem(item),
            },
            ct
        );
    }

    public async Task RemoveFloorItemByIdAsync(long itemId, long pickerId, CancellationToken ct)
    {
        if (_roomMapState is null)
            throw new Exception("Room map state is not initialized.");

        if (!_floorItemsById.Remove(itemId, out var item))
            throw new KeyNotFoundException($"Floor item not found: ItemId={itemId}");

        if (GetTileIdxForFloorItem(item, out var tileIdxs))
        {
            foreach (var idx in tileIdxs)
            {
                _roomMapState.TileFloorStacks[idx].Remove(item.Id);

                ComputeTile(idx);
            }
        }

        _mapVersion++;

        await SendComposerToRoomAsync(
            new ObjectRemoveMessageComposer
            {
                ObjectId = (int)item.Id,
                IsExpired = false,
                PickerId = (int)pickerId,
                Delay = 0,
            },
            ct
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Idx(int x, int y)
    {
        var width = _roomModel?.Width ?? 0;
        var idx = y * width + x;

        if (idx < 0 || idx >= (_roomModel?.Size ?? 0))
            throw new IndexOutOfRangeException($"Tile index {idx} is out of bounds.");

        return idx;
    }

    private bool GetTileIdxForSize(
        int x,
        int y,
        Rotation rotation,
        int width,
        int height,
        out List<int> tileIdxs
    )
    {
        tileIdxs = [];

        if (width > 0 && height > 0)
        {
            if (rotation == Rotation.East || rotation == Rotation.West)
            {
                (width, height) = (height, width);
            }
        }

        for (var minX = x; minX < x + width; minX++)
        {
            for (var minY = y; minY < y + height; minY++)
            {
                tileIdxs.Add(Idx(minX, minY));
            }
        }

        return true;
    }

    private bool GetTileIdxForFloorItem(IRoomFloorItem item, out List<int> tileIdxs) =>
        GetTileIdxForSize(
            item.X,
            item.Y,
            item.Rotation,
            item.Definition.Width,
            item.Definition.Height,
            out tileIdxs
        );

    private void ComputeTile(int idx, bool skipUpdates = false)
    {
        if (_roomModel is null || _roomMapState is null)
            throw new Exception("Room map state is not initialized.");

        var nextHeight = _roomModel.Heights[idx];
        var nextState = _roomModel.States[idx];
        var nextHighestId = (long)-1;
        var floorStack = _roomMapState.TileFloorStacks[idx];

        if (floorStack.Count > 0)
        {
            foreach (var itemId in floorStack)
            {
                var item = _floorItemsById[itemId];

                if (item is null)
                    continue;

                var height = item.Z + item.Definition.StackHeight;

                // special logic if stack helper

                if (height < nextHeight)
                    continue;

                nextHeight = height;
                nextHighestId = itemId;
            }
        }

        nextHeight = Math.Truncate(nextHeight * 1000) / 1000;

        var prevRelative = _roomMapState.TileRelativeHeights[idx];
        var nextRelative = RoomModelCompiler.EncodeHeight(
            nextHeight,
            nextState == (byte)RoomTileStateType.Closed
        );

        _roomMapState.TileHeights[idx] = nextHeight;
        _roomMapState.TileRelativeHeights[idx] = nextRelative;
        _roomMapState.TileStates[idx] = nextState;
        _roomMapState.TileHighestFloorItems[idx] = nextHighestId;

        if (!skipUpdates)
        {
            if (prevRelative != nextRelative)
            {
                if (!_pendingTileIdxUpdates.Contains(idx))
                    _pendingTileIdxUpdates.Add(idx);
            }
        }
    }

    private void ComputeMap()
    {
        if (_roomModel is null || _roomMapState is null)
            throw new Exception("Room map state is not initialized.");

        for (int idx = 0; idx < _roomModel.Size; idx++)
            ComputeTile(idx, true);
    }

    private async Task FlushUpdatesAsync(CancellationToken ct)
    {
        var pendingIdxs = _pendingTileIdxUpdates.ToArray();

        if (pendingIdxs.Length > 0)
        {
            _pendingTileIdxUpdates.Clear();

            var pendingTileUpdates = pendingIdxs
                .Select(idx => new RoomTileSnapshot
                {
                    X = (byte)(idx % _roomModel!.Width),
                    Y = (byte)(idx / _roomModel!.Width),
                    RelativeHeight = _roomMapState!.TileRelativeHeights[idx],
                })
                .ToArray();

            await SendComposerToRoomAsync(
                new HeightMapUpdateMessageComposer { Tiles = [.. pendingTileUpdates] },
                ct
            );
        }
    }

    private async Task BuildMapSnapshotAsync(CancellationToken ct)
    {
        if (_roomModel is null || _roomMapState is null)
            throw new Exception("Room map is not built.");

        var items = new List<RoomFloorItemSnapshot>(_floorItemsById.Count);

        foreach (var stack in _roomMapState.TileFloorStacks)
        {
            for (var i = 0; i < stack.Count; i++)
                items.Add(RoomFloorItemSnapshot.FromFloorItem(_floorItemsById[stack[i]]));
        }

        _roomMapSnapshot = new RoomMapSnapshot
        {
            ModelName = _roomModel.Name,
            ModelData = _roomModel.Model,
            Width = _roomModel.Width,
            Height = _roomModel.Height,
            Size = _roomModel.Size,
            DoorX = _roomModel.DoorX,
            DoorY = _roomModel.DoorY,
            DoorRotation = _roomModel.DoorRotation,
            TileRelativeHeights = [.. _roomMapState.TileRelativeHeights],
            FloorItems = items,
            Version = _mapVersion,
        };
    }

    public Task<RoomSnapshot> GetSnapshotAsync() => Task.FromResult(state.State.RoomSnapshot);

    public async Task<RoomSummarySnapshot> GetSummaryAsync()
    {
        var population = await GetRoomPopulationAsync();

        return new RoomSummarySnapshot
        {
            RoomId = state.State.RoomSnapshot.RoomId,
            Name = state.State.RoomSnapshot.Name,
            Description = state.State.RoomSnapshot.Description,
            OwnerId = state.State.RoomSnapshot.OwnerId,
            OwnerName = state.State.RoomSnapshot.OwnerName,
            Population = population,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task<int> GetRoomPopulationAsync() =>
        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .GetRoomPopulationAsync(this.GetPrimaryKeyLong());

    public async Task<RoomMapSnapshot> GetMapSnapshotAsync()
    {
        await EnsureMapBuiltAsync(CancellationToken.None);

        if (_roomMapSnapshot is null)
            throw new Exception("Room map snapshot is not built.");

        return _roomMapSnapshot;
    }

    public async Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct)
    {
        var roomDirectory = _grainFactory.GetGrain<IRoomDirectoryGrain>(
            RoomDirectoryGrain.SINGLETON_KEY
        );

        await roomDirectory.SendComposerToRoomAsync(composer, this.GetPrimaryKeyLong(), ct);
    }
}
