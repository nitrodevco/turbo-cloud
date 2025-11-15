using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Room;
using Turbo.Primitives.Orleans.Snapshots.Room.Settings;

namespace Turbo.Rooms.Grains;

public class RoomGrain(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IRoomGrain> logger,
    IGrainFactory grainFactory
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IRoomGrain> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;

    private IAsyncStream<RoomEvent>? _stream = null;
    private HashSet<long> _connectedPlayerIds = [];
    private RoomSnapshot? _snapshot = null;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            var provider = this.GetStreamProvider(OrleansStreamProviders.DEFAULT_STREAM_PROVIDER);

            _stream = provider.GetStream<RoomEvent>(
                StreamId.Create(OrleansStreamNames.ROOM_EVENTS, this.GetPrimaryKeyLong())
            );

            await HydrateFromExternalAsync(ct);

            var activeInfo = await GetActiveInfoSnapshotAsync();

            await _grainFactory.GetGrain<IRoomDirectoryGrain>(0).UpsertActiveRoomAsync(activeInfo);

            _logger.LogInformation("RoomGrain:{RoomId} activated.", this.GetPrimaryKeyLong());
        }
        catch
        {
            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        try
        {
            await _grainFactory
                .GetGrain<IRoomDirectoryGrain>(0)
                .MarkInactiveAsync(this.GetPrimaryKeyLong());

            await WriteToDatabaseAsync(ct);

            _logger.LogInformation("RoomGrain:{RoomId} deactivated.", this.GetPrimaryKeyLong());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating RoomGrain:{RoomId}", this.GetPrimaryKeyLong());

            throw;
        }
    }

    protected async Task HydrateFromExternalAsync(CancellationToken ct)
    {
        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity =
            await dbCtx
                .Rooms.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == this.GetPrimaryKeyLong(), ct)
            ?? throw new Exception($"RoomGrain:{this.GetPrimaryKeyLong()} not found in database.");

        _snapshot = new RoomSnapshot
        {
            RoomId = entity.Id,
            RoomName = entity.Name ?? string.Empty,
            OwnerId = (long)entity.PlayerEntityId,
            OwnerName = string.Empty,
            DoorMode = entity.DoorMode,
            UserCount = _connectedPlayerIds.Count,
            PlayersMax = entity.PlayersMax,
            Description = entity.Description ?? string.Empty,
            TradeMode = entity.TradeType,
            Score = 0,
            Ranking = 0,
            CategoryId = entity.NavigatorCategoryEntityId,
            Tags = [],
            AllowPets = entity.AllowPets,
            AllowPetsEat = entity.AllowPetsEat,
            Password = entity.Password ?? string.Empty,
            ModelId = entity.RoomModelEntityId,
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

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync();

        using var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct);

        try
        {
            /* await dbCtx
                .Rooms.Where(r => r.Id == this.GetPrimaryKeyLong())
                .ExecuteUpdateAsync(up => { }, ct); */
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error writing RoomGrain:{RoomId} to database.",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    public Task<ImmutableArray<long>> GetPlayerIdsAsync() =>
        Task.FromResult(_connectedPlayerIds.ToImmutableArray());

    public async Task<bool> AddPlayerIdAsync(long playerId)
    {
        var added = _connectedPlayerIds.Add(playerId);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(0)
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), _connectedPlayerIds.Count);

        return added;
    }

    public async Task<bool> RemovePlayerIdAsync(long playerId)
    {
        var removed = _connectedPlayerIds.Remove(playerId);

        await _grainFactory
            .GetGrain<IRoomDirectoryGrain>(0)
            .UpdatePopulationAsync(this.GetPrimaryKeyLong(), _connectedPlayerIds.Count);

        return removed;
    }

    public Task<RoomSnapshot> GetSnapshotAsync() => Task.FromResult(_snapshot!);

    public Task<RoomActiveInfoSnapshot> GetActiveInfoSnapshotAsync() =>
        Task.FromResult(
            new RoomActiveInfoSnapshot
            {
                RoomId = this.GetPrimaryKeyLong(),
                Population = _connectedPlayerIds.Count,
                Name = _snapshot!.RoomName,
                Description = _snapshot!.Description,
                OwnerId = _snapshot!.OwnerId,
                OwnerName = _snapshot!.OwnerName,
                LastUpdatedUtc = DateTime.UtcNow,
            }
        );
}
