using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Orleans;

namespace Turbo.Admin.Content;

/// <summary>
/// The hotel's groups as staff look them up, and the parts and colours players build group
/// badges from. A group is changed through its grain; a part or colour is added or changed here
/// and the group directory reads them again. Part and colour ids are written into badge codes,
/// so they are only ever added: a file or a colour changes, its id never does.
/// </summary>
public sealed partial class AdminGroupQueries(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
)
{
    public const int PAGE_SIZE = 25;
    public const int MEMBER_LIMIT = 500;

    public async Task<GroupSearchResponse> SearchAsync(
        string? query,
        int page,
        CancellationToken ct
    )
    {
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var groups = dbCtx.Guilds.AsNoTracking();

        if (words.Length > 0)
            groups = int.TryParse(words, out var id)
                ? groups.Where(x => x.Id == id || x.RoomEntityId == id)
                : groups.Where(x => x.Name.Contains(words) || x.PlayerEntity!.Name == words);

        var total = await groups.CountAsync(ct).ConfigureAwait(false);
        var rows = await groups
            .OrderByDescending(x => x.Id)
            .Skip(Math.Max(0, page) * PAGE_SIZE)
            .Take(PAGE_SIZE)
            .Select(x => new GroupItem(
                x.Id,
                x.Name,
                x.BadgeCode,
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.RoomEntityId,
                dbCtx.GuildMembers.Count(m =>
                    m.GuildEntityId == x.Id && m.Rank <= GuildMemberRank.Member
                ),
                x.CreatedAt
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new GroupSearchResponse(rows, total, PAGE_SIZE);
    }

    public async Task<GroupDetailResponse?> GetAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var group = await dbCtx
            .Guilds.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.BadgeCode,
                x.GuildType,
                x.PlayerEntityId,
                OwnerName = x.PlayerEntity!.Name,
                x.RoomEntityId,
                RoomName = x.RoomEntity!.Name,
                x.CreatedAt,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (group is null)
            return null;

        var members = await dbCtx
            .GuildMembers.AsNoTracking()
            .Where(x => x.GuildEntityId == id)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Id)
            .Take(MEMBER_LIMIT)
            .Select(x => new GroupMemberItem(x.PlayerEntityId, x.PlayerEntity!.Name, x.Rank))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new GroupDetailResponse(
            group.Id,
            group.Name,
            group.Description,
            group.BadgeCode,
            group.GuildType,
            group.PlayerEntityId,
            group.OwnerName,
            group.RoomEntityId,
            group.RoomName,
            group.CreatedAt,
            members
        );
    }

    public async Task<GroupEditorResponse> GetEditorAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var parts = await dbCtx
            .GuildBadgeParts.AsNoTracking()
            .OrderBy(x => x.PartType)
            .ThenBy(x => x.PartId)
            .Select(x => new GroupBadgePartItem(
                x.Id,
                x.PartType,
                x.PartId,
                x.FileName,
                x.MaskFileName
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var colors = await dbCtx
            .GuildColors.AsNoTracking()
            .OrderBy(x => x.Slot)
            .ThenBy(x => x.ColorId)
            .Select(x => new GroupColorItem(x.Id, x.Slot, x.ColorId, x.Color))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new GroupEditorResponse(parts, colors);
    }

    /// <summary>Adds a badge part (row id 0) under the next part id of its kind, or changes one's files.</summary>
    public async Task<int> SavePartAsync(
        int id,
        GroupBadgePartRequest request,
        CancellationToken ct
    )
    {
        var file = FileName(request.FileName, required: true);
        var mask = FileName(request.MaskFileName, required: false);
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        GuildBadgePartEntity row;

        if (id == 0)
        {
            var type = request.PartType ?? GuildBadgePartType.Symbol;

            if (!Enum.IsDefined(type))
                throw new ArgumentException("A part is a base or a symbol.");

            var last = await dbCtx
                .GuildBadgeParts.Where(x => x.PartType == type)
                .Select(x => (int?)x.PartId)
                .MaxAsync(ct)
                .ConfigureAwait(false);

            row = new GuildBadgePartEntity
            {
                PartType = type,
                PartId = (last ?? 0) + 1,
                FileName = file,
                MaskFileName = mask,
            };
            dbCtx.GuildBadgeParts.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .GuildBadgeParts.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no badge part {id}.");
            row.FileName = file;
            row.MaskFileName = mask;
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await grainFactory.GetGuildDirectoryGrain().ReloadAsync(ct).ConfigureAwait(false);

        return row.Id;
    }

    /// <summary>Adds a colour (row id 0) under the next colour id of its slot, or changes one's hex.</summary>
    public async Task<int> SaveColorAsync(int id, GroupColorRequest request, CancellationToken ct)
    {
        var color = (request.Color ?? string.Empty).Trim().TrimStart('#').ToLowerInvariant();

        if (!HexPattern().IsMatch(color))
            throw new ArgumentException("A colour is six hex digits, such as ff8800.");

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        GuildColorEntity row;

        if (id == 0)
        {
            var slot = request.Slot ?? GuildColorSlotType.Badge;

            if (!Enum.IsDefined(slot))
                throw new ArgumentException("That is no colour slot.");

            var last = await dbCtx
                .GuildColors.Where(x => x.Slot == slot)
                .Select(x => (int?)x.ColorId)
                .MaxAsync(ct)
                .ConfigureAwait(false);

            row = new GuildColorEntity
            {
                Slot = slot,
                ColorId = (last ?? 0) + 1,
                Color = color,
            };
            dbCtx.GuildColors.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .GuildColors.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no colour {id}.");
            row.Color = color;
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await grainFactory.GetGuildDirectoryGrain().ReloadAsync(ct).ConfigureAwait(false);

        return row.Id;
    }

    private static string FileName(string? name, bool required)
    {
        name = (name ?? string.Empty).Trim();

        if (required && name.Length == 0)
            throw new ArgumentException(
                "A part needs its file: the client loads badgepart_<file>.png."
            );

        if (name.Length > GuildBadgePartEntity.FILE_NAME_MAX_LENGTH || name.Any(char.IsWhiteSpace))
            throw new ArgumentException("A file name is one word, as the image library has it.");

        return name;
    }

    [GeneratedRegex("^[0-9a-f]{6}$")]
    private static partial Regex HexPattern();
}
