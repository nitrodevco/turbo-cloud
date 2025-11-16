using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    IRoomService roomService
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IRoomService _roomService = roomService;

    private IAsyncStream<RoomEvent>? _stream = null;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

        _stream = provider.GetStream<RoomEvent>(
            StreamId.Create(OrleansStreamNames.ROOM_EVENTS, this.GetPrimaryKeyLong())
        );

        await HydrateFromExternalAsync(ct);

        await _roomService.GetRoomDirectory().UpsertActiveRoomAsync(state.State.RoomSnapshot);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await _roomService.GetRoomDirectory().RemoveActiveRoomAsync(this.GetPrimaryKeyLong());

        await WriteToDatabaseAsync(ct);
    }

    public async Task AddPlayerIdAsync(long playerId)
    {
        if (!state.State.PlayerIds.Add(playerId))
            return;

        state.State.LastUpdatedUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        var count = state.State.PlayerIds.Count;

        await _roomService
            .GetRoomDirectory()
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), count);
    }

    public async Task RemovePlayerIdAsync(long playerId)
    {
        if (!state.State.PlayerIds.Remove(playerId))
            return;

        state.State.LastUpdatedUtc = DateTime.UtcNow;

        await state.WriteStateAsync();

        var count = state.State.PlayerIds.Count;

        await _roomService
            .GetRoomDirectory()
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), count);
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

    public Task<ImmutableArray<long>> GetPlayerIdsAsync() =>
        Task.FromResult(state.State.PlayerIds.ToImmutableArray());

    public async Task<RoomSnapshot> GetSnapshotAsync()
    {
        var population = await GetRoomPopulationAsync();

        return state.State.RoomSnapshot with
        {
            Population = population,
        };
    }

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
        await _roomService.GetRoomDirectory().GetRoomPopulationAsync(this.GetPrimaryKeyLong());
}
