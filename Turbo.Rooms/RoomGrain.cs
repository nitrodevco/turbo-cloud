using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Grains;
using Turbo.Primitives.Snapshots.Navigator;
using Turbo.Primitives.Snapshots.Rooms;
using Turbo.Primitives.Snapshots.Rooms.Extensions;
using Turbo.Rooms.Abstractions;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms;

public class RoomGrain(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<IRoomGrain> logger,
    IRoomModelProvider roomModelProvider
) : Grain, IRoomGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<IRoomGrain> _logger = logger;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly RoomState _state = new();

    private RoomSnapshot? _snapshot = null;
    private IRoomMap _roomMap = default!;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateFromExternalAsync(ct);
            await LoadMapAsync(ct);

            _logger.LogInformation("RoomGrain {RoomId} activated.", this.GetPrimaryKeyLong());
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
            await WriteToDatabaseAsync(ct);

            _logger.LogInformation("RoomGrain {RoomId} deactivated.", this.GetPrimaryKeyLong());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating RoomGrain {RoomId}", this.GetPrimaryKeyLong());

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
            ?? throw new Exception(
                $"Room with ID {this.GetPrimaryKeyLong()} not found in database."
            );

        _snapshot = new RoomSnapshot
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            Description = entity.Description ?? string.Empty,
            OwnerId = entity.PlayerEntityId,
            DoorMode = entity.DoorMode,
            Password = entity.Password,
            ModelId = entity.RoomModelEntityId,
            CategoryId = entity.NavigatorCategoryEntityId,
            PlayersMax = entity.PlayersMax,
            AllowPets = entity.AllowPets,
            AllowPetsEat = entity.AllowPetsEat,
            TradeMode = entity.TradeType,
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

        _state.IsLoaded = true;
        _state.LastTick = DateTime.UtcNow;
    }

    protected async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        var snapshot = await GetSnapshotAsync(ct);

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
                "Error writing RoomGrain {RoomId} to database.",
                this.GetPrimaryKeyLong()
            );

            throw;
        }
    }

    protected async Task LoadMapAsync(CancellationToken ct)
    {
        if (_snapshot is null)
            return;

        var model = _roomModelProvider.Current.GetModelById(_snapshot.ModelId);
        _roomMap = new RoomMap(model.Model);
    }

    public ValueTask<RoomSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        ValueTask.FromResult(_snapshot!);
}
