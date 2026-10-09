using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Content;

/// <summary>
/// The navigator's categories as staff edit them: the room categories (<c>navigator_flatcats</c>),
/// the event categories (<c>navigator_eventcats</c>) and the tabs along the navigator's top
/// (<c>navigator_top_level_contexts</c>). Each change reloads the navigator's cache
/// (<see cref="INavigatorProvider.ReloadAsync"/>), and a renamed or removed category drops the
/// listings kept under it, so players see it the next time they open the navigator.
/// </summary>
public sealed class AdminNavigatorEditor(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    INavigatorProvider navigator,
    IGrainFactory grainFactory,
    ILogger<AdminNavigatorEditor> logger
)
{
    public const int NAME_MAX_LENGTH = 100;

    public async Task<NavigatorContentResponse> GetAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rooms = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => x.NavigatorCategoryEntityId != null)
            .GroupBy(x => x.NavigatorCategoryEntityId!.Value)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct)
            .ConfigureAwait(false);
        var events = await dbCtx
            .RoomEvents.AsNoTracking()
            .GroupBy(x => x.NavigatorEventCategoryEntityId)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct)
            .ConfigureAwait(false);
        var flats = await dbCtx
            .NavigatorFlatCategories.AsNoTracking()
            .OrderBy(x => x.OrderNum)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var eventCategories = await dbCtx
            .NavigatorEventCategories.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var contexts = await dbCtx
            .NavigatorTopLevelContexts.AsNoTracking()
            .OrderBy(x => x.OrderNum)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new NavigatorContentResponse(
            [
                .. flats.Select(x => new NavigatorFlatCategoryItem(
                    x.Id,
                    x.Name,
                    x.Visible,
                    x.StaffOnly,
                    x.MinRank,
                    x.RequiredNode,
                    x.OrderNum,
                    x.Automatic,
                    x.AutomaticCategory,
                    x.GlobalCategory,
                    rooms.GetValueOrDefault(x.Id)
                )),
            ],
            [
                .. eventCategories.Select(x => new NavigatorEventCategoryItem(
                    x.Id,
                    x.Name,
                    x.Visible,
                    events.GetValueOrDefault(x.Id)
                )),
            ],
            [
                .. contexts.Select(x => new NavigatorContextItem(
                    x.Id,
                    x.SearchCode,
                    x.Visible,
                    x.OrderNum
                )),
            ]
        );
    }

    /// <summary>Adds a room category (id 0) or changes one.</summary>
    public async Task<int> SaveFlatCategoryAsync(
        int id,
        NavigatorFlatCategoryRequest request,
        CancellationToken ct
    )
    {
        var name = Name(request.Name);
        var node = string.IsNullOrWhiteSpace(request.RequiredNode)
            ? null
            : request.RequiredNode.Trim();

        if (node is not null && !PermissionNodeFormat.IsValidNode(node))
            throw new ArgumentException($"{node} is not a permission node.");

        if (request.MinRank is < 1)
            throw new ArgumentException("The lowest rank is 1, a player's.");

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            await dbCtx
                .NavigatorFlatCategories.AnyAsync(x => x.Name == name && x.Id != id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException(
                $"There is a category {name}: its name is its search code, so it must be its own."
            );

        NavigatorFlatCategoryEntity row;

        if (id == 0)
        {
            row = new NavigatorFlatCategoryEntity
            {
                Name = name,
                Visible = true,
                Automatic = false,
                StaffOnly = false,
                MinRank = 1,
                OrderNum = 0,
            };
            dbCtx.NavigatorFlatCategories.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .NavigatorFlatCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no category {id}.");
        }

        row.Name = name;
        row.Visible = request.Visible ?? row.Visible;
        row.StaffOnly = request.StaffOnly ?? row.StaffOnly;
        row.MinRank = request.MinRank ?? row.MinRank;
        row.RequiredNode = node;
        row.OrderNum = request.OrderNum ?? row.OrderNum;
        row.Automatic = request.Automatic ?? row.Automatic;
        row.AutomaticCategory = Optional(request.AutomaticCategory);
        row.GlobalCategory = Optional(request.GlobalCategory);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync([NavigatorListingKeys.Category(row.Id)], ct).ConfigureAwait(false);

        logger.LogInformation("Navigator category {Id} ({Name}) saved", row.Id, row.Name);

        return row.Id;
    }

    /// <summary>Removes a room category no room is in; refused while rooms are.</summary>
    public async Task<bool> DeleteFlatCategoryAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .NavigatorFlatCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        var rooms = await dbCtx
            .Rooms.CountAsync(x => x.NavigatorCategoryEntityId == id, ct)
            .ConfigureAwait(false);

        if (rooms > 0)
            throw new ArgumentException(
                $"{rooms} rooms are in {row.Name}: move them to another category first."
            );

        dbCtx.NavigatorFlatCategories.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync([NavigatorListingKeys.Category(id)], ct).ConfigureAwait(false);

        logger.LogInformation("Navigator category {Id} ({Name}) removed", id, row.Name);

        return true;
    }

    /// <summary>Adds an event category (id 0) or changes one.</summary>
    public async Task<int> SaveEventCategoryAsync(
        int id,
        NavigatorEventCategoryRequest request,
        CancellationToken ct
    )
    {
        var name = Name(request.Name);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        NavigatorEventCategoryEntity row;

        if (id == 0)
        {
            row = new NavigatorEventCategoryEntity { Name = name, Visible = true };
            dbCtx.NavigatorEventCategories.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .NavigatorEventCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no event category {id}.");
        }

        row.Name = name;
        row.Visible = request.Visible ?? row.Visible;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync([NavigatorListingKeys.EVENTS], ct).ConfigureAwait(false);

        return row.Id;
    }

    /// <summary>Removes an event category no event is in; refused while events are.</summary>
    public async Task<bool> DeleteEventCategoryAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .NavigatorEventCategories.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        if (
            await dbCtx
                .RoomEvents.AnyAsync(x => x.NavigatorEventCategoryEntityId == id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException(
                $"Events are in {row.Name}: hide it instead, or remove it when they are over."
            );

        dbCtx.NavigatorEventCategories.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync([NavigatorListingKeys.EVENTS], ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>Adds a navigator tab by its search code (id 0), or changes one.</summary>
    public async Task<int> SaveContextAsync(
        int id,
        NavigatorContextRequest request,
        CancellationToken ct
    )
    {
        var code = (request.SearchCode ?? string.Empty).Trim();

        if (code.Length is 0 or > NAME_MAX_LENGTH || code.Any(char.IsWhiteSpace))
            throw new ArgumentException("A tab is a search code, such as hotel_view.");

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            await dbCtx
                .NavigatorTopLevelContexts.AnyAsync(x => x.SearchCode == code && x.Id != id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"There is a tab {code} already.");

        NavigatorTopLevelContextEntity row;

        if (id == 0)
        {
            row = new NavigatorTopLevelContextEntity
            {
                SearchCode = code,
                Visible = true,
                OrderNum = 0,
            };
            dbCtx.NavigatorTopLevelContexts.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .NavigatorTopLevelContexts.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no tab {id}.");
        }

        row.SearchCode = code;
        row.Visible = request.Visible ?? row.Visible;
        row.OrderNum = request.OrderNum ?? row.OrderNum;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync([], ct).ConfigureAwait(false);

        return row.Id;
    }

    public async Task<bool> DeleteContextAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .NavigatorTopLevelContexts.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.NavigatorTopLevelContexts.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await ChangedAsync([], ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>The navigator reads its categories again, and drops the listings kept under these keys.</summary>
    private async Task ChangedAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        await navigator.ReloadAsync(ct).ConfigureAwait(false);

        if (keys.Count > 0)
            await grainFactory
                .GetRoomDirectoryGrain()
                .PublishListingChangesAsync(keys, ct)
                .ConfigureAwait(false);
    }

    private static string Name(string? name)
    {
        name = (name ?? string.Empty).Trim();

        if (name.Length is 0 or > NAME_MAX_LENGTH)
            throw new ArgumentException(
                $"A category needs a name, {NAME_MAX_LENGTH} characters at most."
            );

        return name;
    }

    private static string? Optional(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
