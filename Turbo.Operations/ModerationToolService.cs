using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;

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
    ISanctionService sanctionService,
    IRoomService roomService,
    IHotelTextProvider textProvider,
    TimeProvider timeProvider,
    IOptions<OperationsConfig> config,
    ILogger<ModerationToolService> logger
) : IModerationToolService
{
    private const string TIME_FORMAT = "HH:mm";
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm";
    private const string DEFAULT_BAN_REASON = "No reason given";

    /// <summary>"Mute 1h" (<c>ModActionCtrl.as:65</c>): <c>ModMute</c> carries no length.</summary>
    private const int MUTE_MINUTES = 60;

    /// <summary>
    /// A ban's length in hours by its sanction type, as the mod action window names them
    /// (<c>ModActionCtrl.as:66-71</c>): 3 "Ban 18h", 4 "Ban 7 days", 5 and 7 "Ban 30 days" (steps
    /// 1 and 2), 6 "Ban 100 years" (also "Ban avatar-only 100 years").
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> BAN_HOURS = new Dictionary<int, int>
    {
        [3] = 18,
        [4] = 7 * 24,
        [5] = 30 * 24,
        [7] = 30 * 24,
        [6] = 100 * 365 * 24,
    };

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

        var sanctions = dbCtx
            .PlayerSanctions.AsNoTracking()
            .Where(x => x.PlayerEntityId == target.Value);
        var banCount = await sanctions.CountAsync(x => x.Kind == SanctionKind.Ban, ct);
        var cautionCount = await sanctions.CountAsync(x => x.Kind == SanctionKind.Caution, ct);
        var lastSanction = await sanctions
            .OrderByDescending(x => x.Id)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // A trade lock is a denial of "trade" (:tradelock): every one given is in the permission
        // audit, the one in force among the player's nodes.
        var tradeLockCount = await dbCtx
            .PermissionAudit.AsNoTracking()
            .CountAsync(
                x =>
                    x.TargetType == PermissionAuditTargetType.Player
                    && x.TargetId == target.Value
                    && x.Action == PermissionAuditActionType.NodeSet
                    && x.Subject == PermissionNodes.TRADE
                    && x.Value == "false",
                ct
            );
        var tradeLocks = await dbCtx
            .PlayerPermissionNodes.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == target.Value && x.Node == PermissionNodes.TRADE && !x.Value
            )
            .Select(x => x.ExpiresAt)
            .ToListAsync(ct);
        var tradeLockExpiry =
            tradeLocks.Any(x => x is null) ? config.Value.ModerationPermanentTradeLockText
            : tradeLocks.Where(x => x > now).Max() is { } until
                ? until.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)
            : string.Empty;
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
            CautionCount = cautionCount,
            BanCount = banCount,
            TradingLockCount = tradeLockCount,
            TradingExpiryDate = tradeLockExpiry,
            LastPurchaseDate = string.Empty,
            IdentityId = 0,
            IdentityRelatedBanCount = 0,
            PrimaryEmailAddress = string.Empty,
            UserClassification = string.Empty,
            LastSanctionTime = lastSanction is { } at
                ? at.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)
                : string.Empty,
            SanctionAgeHours = lastSanction is { } since
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

        // The live room, not the stored UsersNow, which the room does not keep current.
        var userCount = await grainFactory
            .GetRoomDirectoryGrain()
            .GetRoomPopulationAsync(roomId, ct);

        return new ModeratorRoomInfoSnapshot
        {
            RoomId = roomId.Value,
            UserCount = userCount,
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

        var sent = await grainFactory.TrySendComposerToPlayerAsync(
            target,
            caution
                ? new ModeratorCautionEventMessageComposer { Message = message, Url = string.Empty }
                : new ModeratorMessageComposer { Message = message, Url = string.Empty },
            ct
        );

        // Kept so user info can count them (its "Cautions").
        if (sent && caution)
            await RecordCautionAsync(moderator, target, message, ct);

        return sent;
    }

    public async Task<bool> MessageRoomAsync(
        PlayerId moderator,
        RoomId roomId,
        int actionType,
        string message,
        CancellationToken ct
    )
    {
        // RoomToolCtrl.as:370-377: 0 caution, 1 caution and kick, 3 message, 4 message and kick.
        if (actionType is not (0 or 1 or 3 or 4) || roomId <= 0)
            return false;

        if (string.IsNullOrWhiteSpace(message) || message.Length > config.Value.MaxAlertLength)
            return false;

        // RoomToolCtrl.as:165-177 enables the send buttons on roomAlertPermission alone.
        if (
            !await grainFactory.HasPermissionAsync(moderator, PermissionNodes.Command.ROOMALERT, ct)
        )
            return false;

        var kick = actionType is 1 or 4;

        if (
            kick
            && !await grainFactory.HasPermissionAsync(
                moderator,
                PermissionNodes.Command.ROOMKICKALL,
                ct
            )
        )
            return false;

        var caution = actionType is 0 or 1;
        var players = await grainFactory.GetRoomDirectoryGrain().GetRoomPlayersAsync(roomId, ct);
        var reached = 0;

        foreach (var player in players)
        {
            if (player == moderator)
                continue;

            if (
                await grainFactory.TrySendComposerToPlayerAsync(
                    player,
                    caution
                        ? new ModeratorCautionEventMessageComposer
                        {
                            Message = message,
                            Url = string.Empty,
                        }
                        : new ModeratorMessageComposer { Message = message, Url = string.Empty },
                    ct
                )
            )
                reached++;
        }

        if (kick)
            await grainFactory
                .GetRoomGrain(roomId)
                .ModerateRoomBySystemAsync(moderator, false, null, true, ct);

        return reached > 0;
    }

    public async Task<bool> ModerateRoomAsync(
        PlayerId moderator,
        RoomId roomId,
        bool doorbell,
        bool rename,
        bool kick,
        CancellationToken ct
    )
    {
        if (roomId <= 0 || !(doorbell || rename || kick))
            return false;

        if (
            (doorbell || rename)
            && !await grainFactory.HasPermissionAsync(
                moderator,
                PermissionNodes.Command.ROOMALERT,
                ct
            )
        )
            return false;

        // RoomToolCtrl.as:220-223 disables "kick" without roomKickPermission.
        if (
            kick
            && !await grainFactory.HasPermissionAsync(
                moderator,
                PermissionNodes.Command.ROOMKICKALL,
                ct
            )
        )
            return false;

        return await grainFactory
            .GetRoomGrain(roomId)
            .ModerateRoomBySystemAsync(
                moderator,
                doorbell,
                rename ? config.Value.ModerationInappropriateRoomName : null,
                kick,
                ct
            );
    }

    public async Task<bool> SanctionAsync(
        PlayerId moderator,
        PlayerId target,
        ModSanction sanction,
        CancellationToken ct
    )
    {
        var message = sanction.Message.Trim();

        if (message.Length > config.Value.MaxAlertLength)
            return false;

        if (message.Length == 0)
            message = config.Value.ModerationDefaultSanctionMessage;

        if (sanction.Type == ModSanctionType.Caution)
            return await SendMessageAsync(moderator, target, message, caution: true, ct);

        var node = sanction.Type switch
        {
            ModSanctionType.Mute => PermissionNodes.Command.SILENCE,
            ModSanctionType.Ban => PermissionNodes.Command.BAN,
            ModSanctionType.Kick => PermissionNodes.Room.MODERATE_ANY,
            ModSanctionType.TradeLock => PermissionNodes.Command.TRADELOCK,
            _ => null,
        };

        // The chat command's node, and a player who holds it is staff to it (OperatorGuards).
        if (
            node is null
            || target <= 0
            || target == moderator
            || !await grainFactory.HasPermissionAsync(moderator, node, ct)
            || await grainFactory.HasPermissionAsync(target, node, ct)
        )
            return false;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        bool done;

        switch (sanction.Type)
        {
            case ModSanctionType.Mute:
                done = await DenyAsync(
                    moderator,
                    target,
                    PermissionNodes.Chat.SPEAK,
                    now.AddMinutes(MUTE_MINUTES),
                    ct
                );
                break;
            case ModSanctionType.TradeLock:
                if (sanction.Minutes <= 0)
                    return false;

                done = await DenyAsync(
                    moderator,
                    target,
                    PermissionNodes.TRADE,
                    now.AddMinutes(sanction.Minutes),
                    ct
                );
                break;
            case ModSanctionType.Kick:
                var room = await grainFactory.GetPlayerPresenceGrain(target).GetActiveRoomAsync(ct);

                done =
                    room.RoomId > 0
                    && await roomService.KickPlayerAsync(
                        ActionContext.CreateForPlayer(moderator, room.RoomId),
                        target,
                        ct
                    );
                break;
            case ModSanctionType.Ban:
                return await BanAsync(moderator, target, sanction.BanTypeId, message, now, ct);
            default:
                return false;
        }

        if (done && message.Length > 0)
            await grainFactory.TrySendComposerToPlayerAsync(
                target,
                new ModeratorMessageComposer { Message = message, Url = string.Empty },
                ct
            );

        return done;
    }

    private async Task<bool> BanAsync(
        PlayerId moderator,
        PlayerId target,
        int banTypeId,
        string reason,
        DateTime now,
        CancellationToken ct
    )
    {
        if (!BAN_HOURS.TryGetValue(banTypeId, out var hours))
            return false;

        var ban = await sanctionService.BanAsync(
            target,
            now.AddHours(hours),
            reason.Length > 0 ? reason : DEFAULT_BAN_REASON,
            moderator,
            ct
        );

        // As :ban does: the ban holds whether or not the player was online to be sent out.
        try
        {
            await sessionGateway.DisconnectPlayerAsync(
                target,
                new UserBannedMessageComposer
                {
                    Message = await SanctionMessages.BanMessageAsync(ban, textProvider, ct),
                },
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ban saved but disconnect failed for player {PlayerId}", target);
        }

        return true;
    }

    private async Task<bool> DenyAsync(
        PlayerId moderator,
        PlayerId target,
        string node,
        DateTime expiresAtUtc,
        CancellationToken ct
    ) =>
        await grainFactory
            .GetPlayerPermissionGrain(target)
            .SetNodeAsync(
                node,
                false,
                expiresAtUtc,
                PermissionExpiryModeType.Replace,
                moderator,
                ct
            ) == PermissionChangeResultType.Changed;

    private async Task RecordCautionAsync(
        PlayerId moderator,
        PlayerId target,
        string message,
        CancellationToken ct
    )
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.PlayerSanctions.Add(
            new PlayerSanctionEntity
            {
                PlayerEntityId = target.Value,
                Kind = SanctionKind.Caution,
                Reason =
                    message.Length > PlayerSanctionEntity.REASON_MAX_LENGTH
                        ? message[..PlayerSanctionEntity.REASON_MAX_LENGTH]
                        : message,
                IssuerEntityId = moderator.Value,
                CreatedAt = now,
                ExpiresAt = now,
            }
        );

        await dbCtx.SaveChangesAsync(ct);
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
