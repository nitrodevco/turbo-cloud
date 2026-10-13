using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.Operations;

/// <summary>
/// The moderation tool, read from the database and the players' grains. Which of the tool's
/// actions a moderator is offered is the command node that does the same from chat, so one grant
/// decides both: a caution is <c>command.warn</c>, a kick from the room <c>room.moderate.any</c>,
/// a ban <c>command.ban</c>, a room alert <c>command.roomalert</c>, a room kick
/// <c>command.roomkickall</c>; the chatlogs and the issue queue come with <c>moderation.tool</c>.
/// </summary>
public sealed class ModerationToolService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    TimeProvider timeProvider,
    IOptions<OperationsConfig> config
) : IModerationToolService
{
    private const string TIME_FORMAT = "HH:mm";
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm";

    public async Task<ModeratorInitSnapshot?> GetInitAsync(PlayerId moderator, CancellationToken ct)
    {
        Task<bool> Holds(string node) => grainFactory.HasPermissionAsync(moderator, node, ct);

        if (!await Holds(PermissionNodes.Moderation.TOOL))
            return null;

        return new ModeratorInitSnapshot
        {
            MessageTemplates = [.. config.Value.ModerationMessageTemplates],
            RoomMessageTemplates = [.. config.Value.ModerationRoomMessageTemplates],
            CfhPermission = true,
            ChatlogsPermission = true,
            AlertPermission = await Holds(PermissionNodes.Command.WARN),
            KickPermission = await Holds(PermissionNodes.Room.MODERATE_ANY),
            BanPermission = await Holds(PermissionNodes.Command.BAN),
            RoomAlertPermission = await Holds(PermissionNodes.Command.ROOMALERT),
            RoomKickPermission = await Holds(PermissionNodes.Command.ROOMKICKALL),
        };
    }

    public async Task<ModeratorUserInfoSnapshot?> GetUserInfoAsync(
        PlayerId target,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var player = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == target.Value)
            .Select(x => new
            {
                x.Name,
                x.Figure,
                x.CreatedAt,
                x.LastLoginAt,
            })
            .FirstOrDefaultAsync(ct);

        if (player is null)
            return null;

        var bans = dbCtx
            .PlayerSanctions.AsNoTracking()
            .Where(x => x.PlayerEntityId == target.Value && x.Kind == SanctionKind.Ban);
        var banCount = await bans.CountAsync(ct);
        var lastBan = await bans.OrderByDescending(x => x.Id)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var cfhCount = await dbCtx
            .CfhReports.AsNoTracking()
            .CountAsync(x => x.ReporterEntityId == target.Value, ct);
        var online = sessionGateway.GetOnlinePlayerIds().Contains(target);

        return new ModeratorUserInfoSnapshot
        {
            UserId = target.Value,
            UserName = player.Name,
            Figure = player.Figure,
            RegistrationAgeInMinutes = MinutesSince(player.CreatedAt, now),
            MinutesSinceLastLogin = MinutesSince(player.LastLoginAt ?? player.CreatedAt, now),
            Online = online,
            CfhCount = cfhCount,
            AbusiveCfhCount = 0,
            CautionCount = 0,
            BanCount = banCount,
            TradingLockCount = 0,
            TradingExpiryDate = string.Empty,
            LastPurchaseDate = string.Empty,
            IdentityId = 0,
            IdentityRelatedBanCount = 0,
            PrimaryEmailAddress = string.Empty,
            UserClassification = string.Empty,
            LastSanctionTime = lastBan is { } at
                ? at.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)
                : string.Empty,
            SanctionAgeHours = lastBan is { } since
                ? (int)Math.Min(int.MaxValue, (now - since).TotalHours)
                : int.MaxValue,
        };
    }

    public async Task<ModeratorRoomInfoSnapshot> GetRoomInfoAsync(
        RoomId roomId,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var room = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => x.Id == roomId.Value)
            .Select(x => new
            {
                x.Name,
                x.Description,
                x.Tags,
                x.UsersNow,
                OwnerId = x.PlayerEntityId,
                OwnerName = x.PlayerEntity.Name,
            })
            .FirstOrDefaultAsync(ct);

        if (room is null)
            return new ModeratorRoomInfoSnapshot
            {
                RoomId = roomId.Value,
                UserCount = 0,
                OwnerInRoom = false,
                OwnerId = 0,
                OwnerName = string.Empty,
                Exists = false,
                Name = string.Empty,
                Description = string.Empty,
                Tags = [],
            };

        var ownerRoom = await grainFactory
            .GetPlayerPresenceGrain(room.OwnerId)
            .GetActiveRoomAsync(ct);

        return new ModeratorRoomInfoSnapshot
        {
            RoomId = roomId.Value,
            UserCount = room.UsersNow,
            OwnerInRoom = ownerRoom.RoomId == roomId,
            OwnerId = room.OwnerId,
            OwnerName = room.OwnerName,
            Exists = true,
            Name = room.Name,
            Description = room.Description ?? string.Empty,
            Tags =
            [
                .. (room.Tags ?? string.Empty).Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
            ],
        };
    }

    public async Task<ModChatlogRecordSnapshot> GetRoomChatlogAsync(
        RoomId roomId,
        CancellationToken ct
    )
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var name = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => x.Id == roomId.Value)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);

        return await RoomRecordAsync(
            dbCtx,
            roomId.Value,
            name ?? string.Empty,
            null,
            config.Value.ModerationRoomChatlogLines,
            ct
        );
    }

    public async Task<(
        string UserName,
        ImmutableArray<ModChatlogRecordSnapshot> Records
    )?> GetUserChatlogAsync(PlayerId target, CancellationToken ct)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var name = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == target.Value)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);

        if (name is null)
            return null;

        // The rooms the player last spoke in, the most recent first.
        var rooms = await dbCtx
            .Chatlogs.AsNoTracking()
            .Where(x => x.PlayerEntityId == target.Value)
            .GroupBy(x => x.RoomEntityId)
            .Select(x => new { RoomId = x.Key, Last = x.Max(y => y.Id) })
            .OrderByDescending(x => x.Last)
            .Take(config.Value.ModerationUserChatlogRooms)
            .Select(x => x.RoomId)
            .ToListAsync(ct);
        var names = await dbCtx
            .Rooms.AsNoTracking()
            .Where(x => rooms.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var records = ImmutableArray.CreateBuilder<ModChatlogRecordSnapshot>(rooms.Count);

        foreach (var room in rooms)
            records.Add(
                await RoomRecordAsync(
                    dbCtx,
                    room,
                    names.GetValueOrDefault(room, string.Empty),
                    target.Value,
                    config.Value.ModerationUserChatlogLines,
                    ct
                )
            );

        return (name, records.MoveToImmutable());
    }

    public async Task<bool> SendMessageAsync(
        PlayerId moderator,
        PlayerId target,
        string message,
        bool caution,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > config.Value.MaxAlertLength)
            return false;

        if (
            caution
            && !await grainFactory.HasPermissionAsync(moderator, PermissionNodes.Command.WARN, ct)
        )
            return false;

        return await grainFactory.TrySendComposerToPlayerAsync(
            target,
            caution
                ? new ModeratorCautionEventMessageComposer { Message = message, Url = string.Empty }
                : new ModeratorMessageComposer { Message = message, Url = string.Empty },
            ct
        );
    }

    /// <summary>
    /// A room's latest lines, oldest first: the room's whole chat, or only one player's when
    /// <paramref name="playerId"/> is given.
    /// </summary>
    private static async Task<ModChatlogRecordSnapshot> RoomRecordAsync(
        TurboDbContext dbCtx,
        int roomId,
        string roomName,
        int? playerId,
        int take,
        CancellationToken ct
    )
    {
        var lines = dbCtx.Chatlogs.AsNoTracking().Where(x => x.RoomEntityId == roomId);

        if (playerId is { } only)
            lines = lines.Where(x => x.PlayerEntityId == only);

        var rows = await lines
            .OrderByDescending(x => x.Id)
            .Take(take)
            .Select(x => new
            {
                x.CreatedAt,
                x.PlayerEntityId,
                ChatterName = x.PlayerEntity.Name,
                x.Message,
            })
            .ToListAsync(ct);

        return new ModChatlogRecordSnapshot
        {
            RecordType = ModChatlogRecordSnapshot.ROOM,
            RoomId = roomId,
            RoomName = roomName,
            Lines =
            [
                .. rows.AsEnumerable()
                    .Reverse()
                    .Select(x => new ModChatlogLineSnapshot
                    {
                        Time = x.CreatedAt.ToString(TIME_FORMAT, CultureInfo.InvariantCulture),
                        ChatterId = x.PlayerEntityId,
                        ChatterName = x.ChatterName,
                        Message = x.Message,
                        Highlighted = false,
                    }),
            ],
        };
    }

    private static int MinutesSince(DateTime then, DateTime now) =>
        (int)Math.Clamp((now - then).TotalMinutes, 0, int.MaxValue);
}
