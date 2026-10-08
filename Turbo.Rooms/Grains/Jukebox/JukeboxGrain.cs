using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Sound;
using Turbo.Primitives.Sound.Enums;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.Sound.Snapshots;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Jukebox;

/// <summary>
/// One jukebox's song disks. Write-through: every change commits the disks' rows (which jukebox
/// holds them and in what order) in one save before memory changes or an inventory is told, so
/// there is nothing to flush on deactivation.
/// <para>
/// A disk in a jukebox is its ordinary <c>furniture</c> row, still owned by whoever put it in,
/// pointing at the jukebox through the column a wired chest uses and numbered by
/// <c>held_position</c>; every inventory query already leaves such rows out. Should the
/// jukebox's own row ever go, the database lets go of them and they are back in their owners'
/// inventories.
/// </para>
/// <para>
/// Awaits inventories and the song directory, never a room: the room awaits this grain.
/// </para>
/// </summary>
internal sealed class JukeboxGrain : Grain, IJukeboxGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly RoomConfig _roomConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider;
    private readonly IInventoryFurnitureLoader _furnitureLoader;
    private readonly ILogger<IJukeboxGrain> _logger;

    private readonly JukeboxLiveState _state;

    public JukeboxGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider defsProvider,
        IInventoryFurnitureLoader furnitureLoader,
        ILogger<IJukeboxGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _grainFactory = grainFactory;
        _defsProvider = defsProvider;
        _furnitureLoader = furnitureLoader;
        _logger = logger;

        _state = new() { JukeboxId = RoomObjectId.Parse((int)this.GetPrimaryKeyLong()) };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load jukebox {JukeboxId}", _state.JukeboxId);

            throw;
        }
    }

    public Task<ImmutableArray<SongDiskSnapshot>> GetDisksAsync(CancellationToken ct) =>
        Task.FromResult(Disks());

    public async Task<JukeboxChangeResultSnapshot> AddDiskAsync(
        PlayerId playerId,
        RoomObjectId diskId,
        int slot,
        CancellationToken ct
    )
    {
        if (_state.Disks.Count >= _roomConfig.JukeboxMaxDisks)
            return Refused(JukeboxChangeResultType.Full);

        var held = await _grainFactory
            .GetInventoryGrain(playerId)
            .GetItemSnapshotsAsync([diskId], ct);

        if (held.IsDefaultOrEmpty)
            return Refused(JukeboxChangeResultType.DiskNotHeld);

        var disk = held[0];

        if (!SongDisks.IsSongDisk(disk.Definition))
            return Refused(JukeboxChangeResultType.NotASongDisk);

        var songs = await _grainFactory.GetSongDirectoryGrain().GetSongsAsync([disk.Extra], ct);

        if (songs.IsDefaultOrEmpty)
        {
            _logger.LogWarning(
                "Song disk {DiskId} of player {PlayerId} carries song {SongId}, which does not exist; not put in jukebox {JukeboxId}",
                diskId,
                playerId,
                disk.Extra,
                _state.JukeboxId
            );

            return Refused(JukeboxChangeResultType.UnknownSong);
        }

        var order = _state.Disks.ToList();

        order.Insert(Math.Clamp(slot, 0, order.Count), disk);

        if (!await SaveOrderAsync(order, taken: disk, given: null, playerId, ct))
            return Refused(JukeboxChangeResultType.DiskNotHeld);

        _state.Disks.Clear();
        _state.Disks.AddRange(order);

        await _grainFactory.GetInventoryGrain(playerId).ReleaseFurnitureAsync([diskId], ct);

        return Changed();
    }

    public async Task<JukeboxChangeResultSnapshot> RemoveDiskAsync(int slot, CancellationToken ct)
    {
        if (slot < 0 || slot >= _state.Disks.Count)
            return Refused(JukeboxChangeResultType.NoSuchSlot);

        var disk = _state.Disks[slot];
        var order = _state.Disks.Where((_, index) => index != slot).ToList();

        if (!await SaveOrderAsync(order, taken: null, given: disk, disk.OwnerId, ct))
        {
            // The rows and the list disagree; read the rows again rather than go on with it.
            _state.Disks.Clear();
            await HydrateAsync(ct);

            return Refused(JukeboxChangeResultType.NoSuchSlot);
        }

        _state.Disks.Clear();
        _state.Disks.AddRange(order);

        await _grainFactory.GetInventoryGrain(disk.OwnerId).ReceiveFurnitureAsync([disk], ct);

        return Changed();
    }

    /// <summary>
    /// Writes the playlist as <paramref name="order"/> in one save: the disk taken from
    /// <paramref name="ownerId"/>'s inventory comes in, the one given back goes out, and every
    /// disk held is numbered by its place. False when a row is not where the list says it is
    /// (the disk to take was placed or traded meanwhile, the one to give back is gone).
    /// </summary>
    private async Task<bool> SaveOrderAsync(
        List<FurnitureItemSnapshot> order,
        FurnitureItemSnapshot? taken,
        FurnitureItemSnapshot? given,
        PlayerId ownerId,
        CancellationToken ct
    )
    {
        var jukeboxId = _state.JukeboxId.Value;
        var takenId = taken?.ItemId.Value;
        var givenId = given?.ItemId.Value;
        var owner = ownerId.Value;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .Furnitures.Where(x =>
                x.ChestItemEntityId == jukeboxId
                || (
                    x.Id == takenId
                    && x.PlayerEntityId == owner
                    && x.RoomEntityId == null
                    && x.ChestItemEntityId == null
                )
            )
            .ToDictionaryAsync(x => x.Id, ct);

        if (takenId is { } id && !rows.ContainsKey(id))
            return false;

        if (givenId is { } returned)
        {
            if (!rows.Remove(returned, out var row))
                return false;

            row.ChestItemEntityId = null;
            row.HeldPosition = null;
        }

        for (var position = 0; position < order.Count; position++)
        {
            if (!rows.TryGetValue(order[position].ItemId.Value, out var row))
            {
                _logger.LogError(
                    "Disk {DiskId} is in jukebox {JukeboxId}'s playlist but its row is not held by it",
                    order[position].ItemId,
                    _state.JukeboxId
                );

                return false;
            }

            row.ChestItemEntityId = jukeboxId;
            row.HeldPosition = position;
        }

        await dbCtx.SaveChangesAsync(ct);

        return true;
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var jukeboxId = _state.JukeboxId.Value;
        var rows = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.ChestItemEntityId == jukeboxId)
            .OrderBy(x => x.HeldPosition)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlayerEntityId,
                x.FurnitureDefinitionEntityId,
                x.ExtraData,
                x.CreatedAt,
            })
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            var definition = _defsProvider.TryGetDefinition(row.FurnitureDefinitionEntityId);

            // A row whose definition is gone cannot be played or handed back; leave it, loudly.
            if (definition is null)
            {
                _logger.LogWarning(
                    "Furniture {ItemId} in jukebox {JukeboxId} uses missing definition {DefinitionId}; left out",
                    row.Id,
                    _state.JukeboxId,
                    row.FurnitureDefinitionEntityId
                );

                continue;
            }

            _state.Disks.Add(
                _furnitureLoader
                    .Create(
                        row.Id,
                        row.PlayerEntityId,
                        string.Empty,
                        definition,
                        row.ExtraData,
                        row.CreatedAt
                    )
                    .GetSnapshot()
            );
        }
    }

    private ImmutableArray<SongDiskSnapshot> Disks() =>
        [.. _state.Disks.Select(x => new SongDiskSnapshot { DiskId = x.ItemId, SongId = x.Extra })];

    private JukeboxChangeResultSnapshot Changed() =>
        new() { Result = JukeboxChangeResultType.Done, Disks = Disks() };

    private JukeboxChangeResultSnapshot Refused(JukeboxChangeResultType result) =>
        new() { Result = result, Disks = Disks() };
}
