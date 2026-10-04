using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Admin.Rooms;

/// <summary>
/// Rooms as staff look them up: every room, the invisible ones too, by name, owner or id, and one
/// room in detail. Read-only, so it reads the rows (as the navigator does) and takes what is live
/// (loaded, population, who is inside) from the room directory. Nothing here calls a room grain,
/// so looking at a room that is not loaded does not load it.
/// </summary>
public sealed class AdminRoomQueries(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grainFactory,
    IOptions<AdminConfig> config,
    TimeProvider timeProvider
)
{
    private const string ESCAPE = "\\";

    public async Task<RoomListResponse> SearchAsync(
        string? text,
        RoomSearchMode mode,
        int page,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.RoomSearchPageSize);
        var term = (text ?? string.Empty).Trim();

        if (term.Length > config.Value.RoomSearchMaxLength)
            term = term[..config.Value.RoomSearchMaxLength];

        page = Math.Max(1, page);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rooms = db.Rooms.AsNoTracking();

        if (term.Length > 0)
        {
            // LIKE, so case is ignored the same way on every database; the text is escaped so a
            // % or _ in it is matched as itself.
            var like = term.Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
                .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
                .Replace("_", ESCAPE + "_", StringComparison.Ordinal);

            rooms = mode switch
            {
                RoomSearchMode.Id => int.TryParse(term, out var id)
                    ? rooms.Where(x => x.Id == id)
                    : rooms.Where(x => false),
                RoomSearchMode.Owner => rooms.Where(x =>
                    EF.Functions.Like(x.PlayerEntity.Name, like + "%", ESCAPE)
                ),
                _ => rooms.Where(x => EF.Functions.Like(x.Name, "%" + like + "%", ESCAPE)),
            };
        }

        var total = await rooms.CountAsync(ct).ConfigureAwait(false);
        var rows = await rooms
            .OrderByDescending(x => x.LastActive)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Name,
                OwnerId = x.PlayerEntityId,
                OwnerName = x.PlayerEntity.Name,
                x.DoorMode,
                x.PlayersMax,
                CategoryName = x.NavigatorFlatCategoryEntity != null
                    ? x.NavigatorFlatCategoryEntity.Name
                    : null,
                x.LastActive,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var live = await LivePopulationsAsync(ct).ConfigureAwait(false);

        return new RoomListResponse(
            total,
            page,
            pageSize,
            [
                .. rows.Select(x => new RoomListItem(
                    x.Id,
                    x.Name,
                    x.OwnerId,
                    x.OwnerName,
                    x.DoorMode.ToString(),
                    x.PlayersMax,
                    x.CategoryName,
                    live.ContainsKey(x.Id),
                    live.GetValueOrDefault(x.Id),
                    x.LastActive
                )),
            ]
        );
    }

    /// <summary>One room in detail; null when there is no such room.</summary>
    public async Task<RoomDetailResponse?> GetAsync(int roomId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var entity = await db
            .Rooms.AsNoTracking()
            .Include(x => x.PlayerEntity)
            .Include(x => x.RoomModelEntity)
            .Include(x => x.NavigatorFlatCategoryEntity)
            .FirstOrDefaultAsync(x => x.Id == roomId, ct)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var room = entity.ToSnapshot(
            entity.PlayerEntity.Name,
            entity.RoomModelEntity.Name,
            null,
            now
        );

        var rights = await db
            .RoomRights.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId)
            .OrderBy(x => x.PlayerEntity!.Name)
            .Select(x => new RoomPlayerRef(x.PlayerEntityId, x.PlayerEntity!.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // An expired ban stays as a row until the room tidies it; it no longer bans anyone.
        var bans = await db
            .RoomBans.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId && x.DateExpires > now)
            .OrderBy(x => x.DateExpires)
            .Select(x => new RoomBanItem(x.PlayerEntityId, x.PlayerEntity.Name, x.DateExpires))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var directory = grainFactory.GetRoomDirectoryGrain();
        var isLoaded = (await directory.GetActiveRoomIdsAsync(ct).ConfigureAwait(false)).Contains(
            new RoomId(roomId)
        );
        var inside = isLoaded
            ? await NamedAsync(
                    await directory
                        .GetRoomPlayersAsync(new RoomId(roomId), ct)
                        .ConfigureAwait(false),
                    ct
                )
                .ConfigureAwait(false)
            : [];

        return new RoomDetailResponse(
            entity.Id,
            room.Name,
            room.Description,
            room.OwnerId.Value,
            room.OwnerName,
            room.WorldType,
            entity.NavigatorCategoryEntityId,
            entity.NavigatorFlatCategoryEntity?.Name,
            [.. room.Tags],
            room.DoorMode.ToString(),
            room.Password.Length > 0,
            room.PlayersMax,
            room.TradeType.ToString(),
            room.AllowPets,
            room.AllowPetsEat,
            // Stored as AllowBlocking, read as walk-through, as the settings dialog does.
            room.AllowBlocking,
            room.ModSettings.WhoCanMute.ToString(),
            room.ModSettings.WhoCanKick.ToString(),
            room.ModSettings.WhoCanBan.ToString(),
            room.ChatProtection.ToString(),
            room.HideWalls,
            room.StaffPick,
            room.HiddenByBc,
            room.Score,
            entity.CreatedAt,
            entity.LastActive,
            isLoaded,
            inside,
            rights,
            bans
        );
    }

    /// <summary>The population of every loaded room, by room id.</summary>
    private async Task<Dictionary<int, int>> LivePopulationsAsync(CancellationToken ct)
    {
        var listing = await grainFactory
            .GetRoomDirectoryGrain()
            .GetListingViewAsync(Guid.Empty, 0, includeActiveRooms: true, ct)
            .ConfigureAwait(false);

        return listing.ActiveRooms.ToDictionary(x => x.RoomId.Value, x => x.Population);
    }

    private async Task<List<RoomPlayerRef>> NamedAsync(
        IReadOnlyCollection<PlayerId> players,
        CancellationToken ct
    )
    {
        if (players.Count == 0)
            return [];

        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync([.. players], ct)
            .ConfigureAwait(false);

        return
        [
            .. players
                .Select(x => new RoomPlayerRef(
                    x.Value,
                    names.TryGetValue(x, out var name) ? name : x.ToString()
                ))
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }
}
