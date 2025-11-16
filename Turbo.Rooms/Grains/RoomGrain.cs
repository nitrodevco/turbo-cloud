using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
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
using Turbo.Primitives.Snapshots.Rooms.Extensions;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms.Grains;

public class RoomGrain(
    [PersistentState(OrleansStateNames.ROOM_STATE, OrleansStorageNames.ROOM_STORE)]
        IPersistentState<RoomState> state,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IOptions<RoomConfig> roomConfig,
    IRoomModelProvider roomModelProvider,
    IRoomFloorItemsLoader floorItemsLoader,
    IGrainFactory grainFactory
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly IRoomFloorItemsLoader _floorItemsLoader = floorItemsLoader;
    private readonly IGrainFactory _grainFactory = grainFactory;

    private IAsyncStream<RoomEvent>? _stream = null;
    private IRoomMap? _roomMap = null;

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
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
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
        if (_roomMap is not null)
            return;

        var roomModel =
            _roomModelProvider.Current.GetModelById(state.State.ModelId)
            ?? throw new Exception("Invalid room model");

        _roomMap = new RoomMap(roomModel);

        var floorItems = await _floorItemsLoader.LoadByRoomIdAsync(this.GetPrimaryKeyLong(), ct);

        foreach (var item in floorItems)
            _roomMap.AddFloorItem(item);
    }

    public async Task MoveFloorItemAsync(
        long itemId,
        int newX,
        int newY,
        Rotation newRotation,
        CancellationToken ct
    )
    {
        await EnsureMapBuiltAsync(ct);

        if (_roomMap is null)
            throw new Exception("Room map is not built.");

        if (!_roomMap.MoveFloorItem(itemId, newX, newY, newRotation, out var item))
            return;

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(RoomDirectoryGrain.SINGLETON_KEY)
            .SendComposerToRoomAsync(
                new ObjectUpdateMessageComposer
                {
                    FloorItem = RoomFloorItemSnapshot.FromFloorItem(item),
                },
                this.GetPrimaryKeyLong(),
                ct
            );
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

        if (_roomMap is null)
            throw new Exception("Room map is not built.");

        return new RoomMapSnapshot
        {
            ModelName = _roomMap.ModelName,
            ModelData = _roomMap.ModelData,
            Width = _roomMap.Width,
            Height = _roomMap.Height,
            Size = _roomMap.Size,
            DoorX = _roomMap.DoorX,
            DoorY = _roomMap.DoorY,
            DoorRotation = _roomMap.DoorRotation,
            TileRelativeHeights = _roomMap.TileRelativeHeights,
            FloorItems = _roomMap.GetAllFloorItems(),
        };
    }

    public async Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync()
    {
        await EnsureMapBuiltAsync(CancellationToken.None);

        if (_roomMap is null)
            throw new Exception("Room map is not built.");

        return _roomMap.GetAllFloorItems();
    }
}
