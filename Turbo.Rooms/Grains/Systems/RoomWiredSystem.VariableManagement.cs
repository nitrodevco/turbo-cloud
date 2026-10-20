using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired.WebApi;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The wired menu's Variable Management (Flash <c>VariableManagementOverviewView</c> and
/// <c>VariableManagementDetailView</c>): who holds a permanent user variable, page by page, and
/// what one user, pet or bot holds, editable. It is offered for a stored user variable only
/// (<c>WiredMenuOverviewTab.canManageCurrentVariable</c>: target user, persisted, no execution
/// context). Holders are named as the Web API names them, a player by player id whether or not
/// they are here, a pet or bot by its id while it is, and the changes come from outside the room
/// as the Web API's do (<c>trigger_origin.3</c>: "Variable Management or Variable Web API"), so
/// both go through the same reads and writes.
/// </summary>
public sealed partial class RoomWiredSystem
{
    /// <summary>
    /// The holder kinds by the client's user type (<c>wiredfurni.params.usertype.&lt;n&gt;</c>: 1
    /// Habbo, 2 Pet, 4 Bot); -1 asks for all of them.
    /// </summary>
    private static readonly (RoomObjectType Type, string Kind)[] ManagedKinds =
    [
        (RoomObjectType.Player, WiredWebApiEntity.USERS),
        (RoomObjectType.Pet, WiredWebApiEntity.PETS),
        (RoomObjectType.Bot, WiredWebApiEntity.BOTS),
    ];

    /// <summary>
    /// <c>wiredmenu.variable_management.sort_by.0</c> to <c>.5</c>: highest value, lowest value,
    /// oldest creation, latest creation, oldest update, latest update.
    /// </summary>
    private const int MANAGEMENT_SORT_TYPES = 6;

    private static string? KindOfEntityType(int entityType) =>
        ManagedKinds.FirstOrDefault(x => (int)x.Type == entityType).Kind;

    /// <summary>
    /// Whether Variable Management may show the variable: a user variable kept beyond the visit
    /// (persisted, shared or a reference to one, <c>WiredVariable.isPersisted</c>) that is not a
    /// pointer, which only has a target while a stack runs.
    /// </summary>
    private static bool IsManaged(IWiredVariable? variable) =>
        variable is FurnitureWiredVariableLogic
        && variable.GetVarSnapshot() is var snapshot
        && snapshot.TargetType == WiredVariableTargetType.User
        && !snapshot.Flags.Has(WiredVariableFlags.RequiresExecutionContext)
        && snapshot.AvailabilityType
            is WiredAvailabilityType.Persistent
                or WiredAvailabilityType.Shared
                or WiredAvailabilityType.Reference;

    /// <summary>
    /// One page of a managed variable's holders, among the user types asked for, sorted as asked
    /// (ties by user type, then id; inference). A page below 1 is page 1, a sort or user type the
    /// client does not offer is the first sort and every type, and the page answers with the
    /// size asked for, as the client drops a page of any other size; at most
    /// <see cref="MAX_PAGE_SIZE"/> holders are on it. Null for a variable it does not manage.
    /// </summary>
    public async Task<WiredVariableOwnersPageSnapshot?> GetVariableOwnersPageAsync(
        WiredVariableId variableId,
        int page,
        int pageSize,
        int sortType,
        int userTypeFilter,
        CancellationToken ct
    )
    {
        var found = GetVariableById(variableId);

        if (!IsManaged(found))
            return null;

        var variable = (FurnitureWiredVariableLogic)found!;

        page = Math.Max(page, 1);
        sortType = sortType is >= 0 and < MANAGEMENT_SORT_TYPES ? sortType : 0;
        userTypeFilter = KindOfEntityType(userTypeFilter) is null ? -1 : userTypeFilter;

        var take = Math.Clamp(pageSize, 1, MAX_PAGE_SIZE);
        var holders = ManagedKinds
            .Where(x => userTypeFilter < 0 || (int)x.Type == userTypeFilter)
            .SelectMany(x =>
                HoldersOf(variable, x.Kind)
                    .Select(holder =>
                        (
                            x.Type,
                            holder.ApiId,
                            holder.Value,
                            Created: holder.CreatedAtMs,
                            Updated: holder.UpdatedAtMs
                        )
                    )
            )
            .ToList();
        Func<
            (RoomObjectType Type, int ApiId, WiredVariableValue Value, long Created, long Updated),
            long
        > order = sortType switch
        {
            0 or 1 => holder => holder.Value.Value,
            2 or 3 => holder => holder.Created,
            _ => holder => holder.Updated,
        };
        // Highest value, latest creation and latest update come first.
        var sorted = sortType is 0 or 3 or 5
            ? holders.OrderByDescending(order)
            : holders.OrderBy(order);
        var onPage = sorted
            .ThenBy(holder => holder.Type)
            .ThenBy(holder => holder.ApiId)
            .Skip((page - 1) * take)
            .Take(take)
            .ToList();
        var absentNames = await GetAbsentPlayerNamesAsync(
            [.. onPage.Where(x => x.Type == RoomObjectType.Player).Select(x => x.ApiId)],
            ct
        );
        var stringId = variableId.ToString();

        return new WiredVariableOwnersPageSnapshot
        {
            VariableId = stringId,
            TotalEntries = holders.Count,
            Page = page,
            PageSize = pageSize,
            Owners =
            [
                .. onPage.Select(holder => new WiredVariableOwnerSnapshot
                {
                    EntityType = (int)holder.Type,
                    EntityId = holder.ApiId,
                    EntityName =
                        NameHere(holder.Type, holder.ApiId)
                        ?? absentNames.GetValueOrDefault(new PlayerId(holder.ApiId))
                        ?? string.Empty,
                    Value = StoredValue(stringId, holder.Value, holder.Created, holder.Updated),
                }),
            ],
            UserTypeFilter = userTypeFilter,
            SortType = sortType,
        };
    }

    /// <summary>
    /// Every managed variable one user, pet or bot holds, with who it is. A player is found here
    /// or, away, while a permanent variable of this room keeps something for them; a pet or bot
    /// only while it is here. A pet or bot names its owner. Null when the entity is none of those.
    /// </summary>
    public async Task<WiredEntityPermanentVariablesSnapshot?> GetEntityPermanentVariablesAsync(
        int entityType,
        int entityId,
        CancellationToken ct
    )
    {
        if (
            KindOfEntityType(entityType) is not { } kind
            || ResolveEntity(kind, entityId.ToString(CultureInfo.InvariantCulture))
                is not { } entity
            || (!entity.IsHere && (kind != WiredWebApiEntity.USERS || !HasUserProfile(entityId)))
        )
            return null;

        var variables = new List<WiredStoredVariableValueSnapshot>();

        foreach (var variable in GetAllVariables())
        {
            if (
                !IsManaged(variable)
                || !TryReadWebApiValue(
                    (FurnitureWiredVariableLogic)variable,
                    entity,
                    out var value,
                    out var createdAtMs,
                    out var updatedAtMs
                )
            )
                continue;

            variables.Add(
                StoredValue(
                    variable.GetVarSnapshot().VariableId.ToString(),
                    value,
                    createdAtMs,
                    updatedAtMs
                )
            );
        }

        var avatar =
            entity.IsHere
            && AvatarModule.TryGetAvatar(RoomObjectId.Parse(entity.LiveId), out var here)
                ? here
                : null;
        var snapshot = new WiredEntityPermanentVariablesSnapshot
        {
            EntityType = entityType,
            EntityId = entityId,
            EntityName = avatar?.Name ?? string.Empty,
            EntityFigure = avatar?.Figure ?? string.Empty,
            Variables = [.. variables],
        };

        if (avatar is null)
        {
            var summary = await _roomGrain
                ._grainFactory.GetPlayerGrain(new PlayerId(entityId))
                .GetSummaryAsync(ct);

            return snapshot with
            {
                EntityName = summary.Name,
                EntityFigure = summary.Figure,
            };
        }

        var (ownerId, ownerName) = avatar switch
        {
            IRoomPet pet => (pet.OwnerId, pet.OwnerName),
            IRoomBot bot => (bot.OwnerId, bot.OwnerName),
            _ => (new PlayerId(0), string.Empty),
        };

        if (ownerId.Value <= 0)
            return snapshot;

        var ownerFigure = AvatarModule.TryGetPlayer(ownerId, out var owner)
            ? owner.Figure
            : (await _roomGrain._grainFactory.GetPlayerGrain(ownerId).GetSummaryAsync(ct)).Figure;

        return snapshot with
        {
            OwnerId = ownerId.Value,
            OwnerName = ownerName,
            OwnerFigure = ownerFigure,
        };
    }

    /// <summary>
    /// Variable Management edits, gives or takes away a managed variable on one user, pet or bot,
    /// as the Web API would (<see cref="WriteWebApiValueAsync"/>, <see cref="RemoveWebApiValue"/>):
    /// an edit needs a held value the variable lets be written, a give or take a variable that can
    /// be given and taken, and an away player one this room keeps something permanent for.
    /// </summary>
    public async Task<bool> ApplyVariableManagementChangeAsync(
        int entityType,
        int entityId,
        WiredVariableId variableId,
        WiredVariableMenuOperationType operation,
        WiredVariableValue value,
        CancellationToken ct
    )
    {
        var found = GetVariableById(variableId);

        if (
            !IsManaged(found)
            || KindOfEntityType(entityType) is not { } kind
            || ResolveEntity(kind, entityId.ToString(CultureInfo.InvariantCulture))
                is not { } entity
        )
            return false;

        var variable = (FurnitureWiredVariableLogic)found!;

        ChangeOrigin = WiredVariableChangeOriginType.External;

        try
        {
            return operation switch
            {
                WiredVariableMenuOperationType.SetValue => await WriteWebApiValueAsync(
                    variable,
                    entity,
                    value,
                    give: false,
                    ct
                )
                    is null,
                WiredVariableMenuOperationType.Create => await WriteWebApiValueAsync(
                    variable,
                    entity,
                    value,
                    give: true,
                    ct
                )
                    is null,
                WiredVariableMenuOperationType.Delete => RemoveWebApiValue(
                    variable,
                    entity,
                    WebApiFlags(variable)
                )
                    is null,
                _ => false,
            };
        }
        finally
        {
            ChangeOrigin = WiredVariableChangeOriginType.ThisRoom;
        }
    }

    /// <summary>The name of a player, pet or bot that is in the room, by its id; null when it is not.</summary>
    private string? NameHere(RoomObjectType type, int id) =>
        type switch
        {
            RoomObjectType.Player => AvatarModule.TryGetPlayer(new PlayerId(id), out var player)
                ? player.Name
                : null,
            RoomObjectType.Pet => AvatarModule
                .Avatars.OfType<IRoomPet>()
                .FirstOrDefault(x => x.PetId == id)
                ?.Name,
            RoomObjectType.Bot => AvatarModule
                .Avatars.OfType<IRoomBot>()
                .FirstOrDefault(x => x.BotId == id)
                ?.Name,
            _ => null,
        };

    private async Task<ImmutableDictionary<PlayerId, string>> GetAbsentPlayerNamesAsync(
        List<int> playerIds,
        CancellationToken ct
    )
    {
        var absent = playerIds
            .Where(id => !AvatarModule.TryGetPlayer(new PlayerId(id), out _))
            .Select(id => new PlayerId(id))
            .ToList();

        return absent.Count == 0
            ? ImmutableDictionary<PlayerId, string>.Empty
            : await _roomGrain
                ._grainFactory.GetPlayerDirectoryGrain()
                .GetPlayerNamesAsync(absent, ct);
    }

    /// <summary>
    /// A held value as the menu lists it, its times also printed as <c>dd-MM-yyyy HH:mm:ss</c>
    /// (official client capture, WIN63-202610201032: "20-10-2026 22:52:00" for 1792533120544, an
    /// hour ahead of UTC). Printed in the room's wired timezone, as the room log prints its own
    /// (inference: which clock Habbo uses is not known); a time the value does not know is left
    /// blank.
    /// </summary>
    private WiredStoredVariableValueSnapshot StoredValue(
        string variableId,
        WiredVariableValue value,
        long createdAtMs,
        long updatedAtMs
    ) =>
        new()
        {
            VariableId = variableId,
            Value = value.ToClient(),
            CreatedAtMs = createdAtMs,
            CreatedAtText = TimeText(createdAtMs),
            UpdatedAtMs = updatedAtMs,
            UpdatedAtText = TimeText(updatedAtMs),
        };

    private string TimeText(long epochMs) =>
        epochMs <= 0
            ? string.Empty
            : TimeZoneInfo
                .ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(epochMs), GetRoomTimeZone())
                .ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
}
