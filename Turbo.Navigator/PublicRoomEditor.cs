using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Navigator;

/// <summary>
/// <see cref="IPublicRoomEditor"/> over <c>navigator_public_categories</c> and
/// <c>navigator_public_rooms</c>. Room order is kept as 0..n-1 in each category, rewritten on
/// every change, so "first" is always order 0.
/// </summary>
public sealed class PublicRoomEditor(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    ILogger<IPublicRoomEditor> logger
) : IPublicRoomEditor
{
    public async Task<ImmutableArray<PublicCategoryEntry>> ListAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var categories = await dbCtx
            .NavigatorPublicCategories.AsNoTracking()
            .OrderBy(x => x.OrderNum)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var members = (
            await dbCtx
                .NavigatorPublicRooms.AsNoTracking()
                .OrderBy(x => x.OrderNum)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.CategoryEntityId,
                    Entry = new PublicRoomEntry(
                        RoomId.Parse(x.RoomEntityId),
                        x.RoomEntity.Name,
                        PlayerId.Parse(x.RoomEntity.PlayerEntityId),
                        x.RoomEntity.PlayerEntity.Name,
                        x.CreatedAt,
                        x.AddedBy == null ? null : PlayerId.Parse(x.AddedBy.Value)
                    ),
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToLookup(x => x.CategoryEntityId, x => x.Entry);
        var picks = categories.Any(x => x.Kind == NavigatorPublicCategoryKind.StaffPicks)
            ? await dbCtx
                .Rooms.AsNoTracking()
                .Where(x => x.StaffPick)
                .OrderByDescending(x => x.StaffPickedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => new PublicRoomEntry(
                    RoomId.Parse(x.Id),
                    x.Name,
                    PlayerId.Parse(x.PlayerEntityId),
                    x.PlayerEntity.Name,
                    x.StaffPickedAt ?? x.CreatedAt,
                    null
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : [];

        return
        [
            .. categories.Select(x => new PublicCategoryEntry(
                x.Id,
                x.Name,
                x.OrderNum,
                x.ForceClosed,
                x.ViewMode,
                x.Visible,
                x.Kind,
                x.PluginKey,
                x.Kind == NavigatorPublicCategoryKind.StaffPicks
                    ? NavigatorSearchCodes.STAFF_PICKS
                    : NavigatorSearchCodes.Official(x.Id),
                x.Kind == NavigatorPublicCategoryKind.StaffPicks ? [.. picks] : [.. members[x.Id]]
            )),
        ];
    }

    public async Task<int> SaveCategoryAsync(
        int id,
        PublicCategoryChange change,
        NavigatorPublicCategoryKind kind,
        string? pluginKey,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        NavigatorPublicCategoryEntity row;

        if (id == 0)
        {
            if (!Enum.IsDefined(kind))
                throw new ArgumentException($"{kind} is not a kind of public category.");

            if (
                kind == NavigatorPublicCategoryKind.StaffPicks
                && await dbCtx
                    .NavigatorPublicCategories.AnyAsync(x => x.Kind == kind, ct)
                    .ConfigureAwait(false)
            )
                throw new ArgumentException("There is a staff picks category already.");

            var key = string.IsNullOrWhiteSpace(pluginKey) ? null : pluginKey.Trim();

            if (key is { Length: > NavigatorPublicCategoryEntity.PLUGIN_KEY_MAX_LENGTH })
                throw new ArgumentException("A plugin key is 100 characters at most.");

            if (
                key is not null
                && await dbCtx
                    .NavigatorPublicCategories.AnyAsync(x => x.PluginKey == key, ct)
                    .ConfigureAwait(false)
            )
                throw new ArgumentException($"There is a category {key} already.");

            var last = await dbCtx
                .NavigatorPublicCategories.MaxAsync(x => (int?)x.OrderNum, ct)
                .ConfigureAwait(false);

            row = new NavigatorPublicCategoryEntity
            {
                Name = string.Empty,
                Kind = kind,
                PluginKey = key,
                OrderNum = (last ?? -1) + 1,
            };
            dbCtx.NavigatorPublicCategories.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .NavigatorPublicCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no public category {id}.");
        }

        row.Name = Name(change.Name ?? (id == 0 ? null : row.Name), row.Kind);
        row.Visible = change.Visible ?? row.Visible;
        row.ForceClosed = change.ForceClosed ?? row.ForceClosed;

        if (change.ViewMode is { } viewMode)
            row.ViewMode = Enum.IsDefined(viewMode)
                ? viewMode
                : throw new ArgumentException($"{viewMode} is not a view mode.");

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Public category {Id} ({Name}) saved", row.Id, row.Name);

        return row.Id;
    }

    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var removed = await dbCtx
            .NavigatorPublicCategories.Where(x => x.Id == id)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (removed == 0)
            return false;

        await ChangedAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Public category {Id} removed", id);

        return true;
    }

    public async Task OrderCategoriesAsync(IReadOnlyList<int> categoryIds, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx.NavigatorPublicCategories.ToListAsync(ct).ConfigureAwait(false);

        Reorder(rows, x => x.Id, categoryIds, (x, i) => x.OrderNum = i, "category");

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> AddRoomAsync(
        int categoryId,
        RoomId roomId,
        bool first,
        PlayerId? by,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        await ManualCategoryAsync(dbCtx, categoryId, ct).ConfigureAwait(false);

        if (!await dbCtx.Rooms.AnyAsync(x => x.Id == roomId.Value, ct).ConfigureAwait(false))
            throw new ArgumentException($"There is no room {roomId.Value}.");

        var rooms = await RoomsOfAsync(dbCtx, categoryId, ct).ConfigureAwait(false);

        if (rooms.Any(x => x.RoomEntityId == roomId.Value))
            return false;

        var row = new NavigatorPublicRoomEntity
        {
            CategoryEntityId = categoryId,
            RoomEntityId = roomId.Value,
            AddedBy = by?.Value,
        };

        dbCtx.NavigatorPublicRooms.Add(row);
        rooms.Insert(first ? 0 : rooms.Count, row);
        Renumber(rooms);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(ct).ConfigureAwait(false);

        return true;
    }

    public async Task MoveRoomAsync(
        int fromCategoryId,
        RoomId roomId,
        int toCategoryId,
        int? index,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        await ManualCategoryAsync(dbCtx, toCategoryId, ct).ConfigureAwait(false);

        var from = await RoomsOfAsync(dbCtx, fromCategoryId, ct).ConfigureAwait(false);
        var row =
            from.FirstOrDefault(x => x.RoomEntityId == roomId.Value)
            ?? throw new ArgumentException(
                $"Room {roomId.Value} is not in public category {fromCategoryId}."
            );

        if (fromCategoryId == toCategoryId)
        {
            from.Remove(row);
            from.Insert(Math.Clamp(index ?? 0, 0, from.Count), row);
            Renumber(from);
        }
        else
        {
            var to = await RoomsOfAsync(dbCtx, toCategoryId, ct).ConfigureAwait(false);

            if (to.Any(x => x.RoomEntityId == roomId.Value))
                throw new ArgumentException(
                    $"Room {roomId.Value} is in public category {toCategoryId} already."
                );

            from.Remove(row);
            row.CategoryEntityId = toCategoryId;
            to.Insert(Math.Clamp(index ?? 0, 0, to.Count), row);
            Renumber(from);
            Renumber(to);
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(ct).ConfigureAwait(false);
    }

    public async Task OrderRoomsAsync(
        int categoryId,
        IReadOnlyList<RoomId> roomIds,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rooms = await RoomsOfAsync(dbCtx, categoryId, ct).ConfigureAwait(false);

        Reorder(
            rooms,
            x => x.RoomEntityId,
            [.. roomIds.Select(x => x.Value)],
            (x, i) => x.OrderNum = i,
            "room"
        );

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RemoveRoomAsync(int categoryId, RoomId roomId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rooms = await RoomsOfAsync(dbCtx, categoryId, ct).ConfigureAwait(false);
        var row = rooms.FirstOrDefault(x => x.RoomEntityId == roomId.Value);

        if (row is null)
            return false;

        dbCtx.NavigatorPublicRooms.Remove(row);
        rooms.Remove(row);
        Renumber(rooms);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync(ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>The category rooms are put in by hand; a staff picks category lists the flagged rooms instead.</summary>
    private static async Task ManualCategoryAsync(
        TurboDbContext dbCtx,
        int categoryId,
        CancellationToken ct
    )
    {
        var kind =
            await dbCtx
                .NavigatorPublicCategories.Where(x => x.Id == categoryId)
                .Select(x => (NavigatorPublicCategoryKind?)x.Kind)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false)
            ?? throw new ArgumentException($"There is no public category {categoryId}.");

        if (kind == NavigatorPublicCategoryKind.StaffPicks)
            throw new ArgumentException(
                "The staff picks category lists the staff-picked rooms: pick the room instead."
            );
    }

    private static Task<List<NavigatorPublicRoomEntity>> RoomsOfAsync(
        TurboDbContext dbCtx,
        int categoryId,
        CancellationToken ct
    ) =>
        dbCtx
            .NavigatorPublicRooms.Where(x => x.CategoryEntityId == categoryId)
            .OrderBy(x => x.OrderNum)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    private static void Renumber(List<NavigatorPublicRoomEntity> rooms)
    {
        for (var i = 0; i < rooms.Count; i++)
            rooms[i].OrderNum = i;
    }

    /// <summary>Applies an order that names every row once, or refuses it.</summary>
    private static void Reorder<T>(
        List<T> rows,
        Func<T, int> id,
        IReadOnlyList<int> order,
        Action<T, int> set,
        string what
    )
    {
        var byId = rows.ToDictionary(id);

        if (
            order.Count != rows.Count
            || order.Distinct().Count() != order.Count
            || !order.All(byId.ContainsKey)
        )
            throw new ArgumentException($"Name every {what} once, in the new order.");

        for (var i = 0; i < order.Count; i++)
            set(byId[order[i]], i);
    }

    private Task ChangedAsync(CancellationToken ct) =>
        grainFactory
            .GetRoomDirectoryGrain()
            .PublishListingChangesAsync([NavigatorListingKeys.PUBLIC], ct);

    /// <summary>A manual category needs a name; the staff picks one may have none, so the client titles it "Staff Picks".</summary>
    private static string Name(string? name, NavigatorPublicCategoryKind kind)
    {
        name = (name ?? string.Empty).Trim();

        if (
            name.Length > NavigatorPublicCategoryEntity.NAME_MAX_LENGTH
            || (name.Length == 0 && kind != NavigatorPublicCategoryKind.StaffPicks)
        )
            throw new ArgumentException(
                $"A category needs a name, {NavigatorPublicCategoryEntity.NAME_MAX_LENGTH} characters at most."
            );

        return name;
    }
}
