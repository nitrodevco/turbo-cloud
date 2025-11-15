using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using Turbo.Contracts.Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans.Events.Rooms;
using Turbo.Primitives.Orleans.Grains.Room;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Orleans.States.Room;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Grains;

public class RoomGrain(
    [PersistentState(OrleansStateNames.ROOM_STATE, OrleansStorageNames.ROOM_STORE)]
        IPersistentState<RoomState> state,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IRoomGrain> logger,
    IRoomService roomService
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IRoomGrain> _logger = logger;
    private readonly IRoomService _roomService = roomService;

    private IAsyncStream<RoomEvent>? _stream = null;
    private RoomSnapshot? _snapshot = null;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

        _stream = provider.GetStream<RoomEvent>(
            StreamId.Create(OrleansStreamNames.ROOM_EVENTS, this.GetPrimaryKeyLong())
        );

        await HydrateFromExternalAsync(ct);

        await _roomService.GetRoomDirectory().UpsertActiveRoomAsync(_snapshot!);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await _roomService.GetRoomDirectory().RemoveActiveRoomAsync(this.GetPrimaryKeyLong());

        await WriteToDatabaseAsync(ct);
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
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

            _snapshot = new RoomSnapshot
            {
                RoomId = entity.Id,
                RoomName = entity.Name ?? string.Empty,
                OwnerId = (long)entity.PlayerEntityId,
                OwnerName = string.Empty,
                DoorMode = entity.DoorMode,
                Population = 0,
                PlayersMax = entity.PlayersMax,
                Description = entity.Description ?? string.Empty,
                TradeMode = entity.TradeType,
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
            };
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

    public async Task<RoomSnapshot> GetSnapshotAsync()
    {
        var population = await GetRoomPopulationAsync();

        if (_snapshot is null)
            throw new Exception($"RoomGrain:{this.GetPrimaryKeyLong()} snapshot is null.");

        return _snapshot with
        {
            Population = population,
        };
    }

    public async Task<RoomSummarySnapshot> GetSummaryAsync()
    {
        var snapshot = _snapshot!;
        var population = await GetRoomPopulationAsync();

        return new RoomSummarySnapshot
        {
            RoomId = snapshot.RoomId,
            Population = population,
            Name = snapshot.RoomName,
            Description = snapshot.Description,
            OwnerId = snapshot.OwnerId,
            OwnerName = snapshot.OwnerName,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    public async Task<int> GetRoomPopulationAsync() =>
        await _roomService.GetRoomDirectory().GetRoomPopulationAsync(this.GetPrimaryKeyLong());
}
