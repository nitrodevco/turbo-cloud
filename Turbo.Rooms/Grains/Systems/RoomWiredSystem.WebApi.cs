using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.Rooms.Wired.WebApi;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.WebApi;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// The room's side of the public Web API's Wired Variables endpoints (Habbo's OpenAPI document,
/// tag "Wired Variables"), opened by a "Variable Global Add-on: Web API" in the room. Every call
/// carries the add-on's read or write key; the variables are the room's variable boxes, by name
/// and target: users, pets and bots for a user variable, floor and wall furni (Builders Club ones
/// apart) for a furni variable, and the room for a global one. A player is named by player id
/// whether or not they are here: away, they hold what a permanent variable keeps for them. The
/// changes come from outside the room, as Variable Management's do (<c>trigger_origin.3</c>:
/// "Variable Management or Variable Web API").
/// </summary>
public sealed partial class RoomWiredSystem
{
    private const int MAX_BATCH_OPERATIONS = 50;
    private const int DEFAULT_PAGE_SIZE = 50;
    private const int MAX_PAGE_SIZE = 100;

    private readonly Dictionary<
        (WiredVariableId, string),
        (int Count, long ExpiresAtMs)
    > _webApiCounts = [];

    private long _webApiBudgetSinceMs;
    private int _webApiBudgetSpent;

    public async Task<WiredWebApiResponse> HandleWebApiAsync(
        WiredWebApiRequest request,
        CancellationToken ct
    )
    {
        var (addon, refusal) = await OpenWebApiAsync(
            request.Operation,
            request.ReadKey,
            request.WriteKey,
            ct
        );

        if (refusal is not null)
            return WiredWebApiJson.Error(WiredWebApiErrors.StatusOf(refusal), refusal);

        ChangeOrigin = WiredVariableChangeOriginType.External;

        try
        {
            return await DispatchWebApiAsync(request, addon!, ct);
        }
        catch (JsonException)
        {
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_VALUE);
        }
        finally
        {
            ChangeOrigin = WiredVariableChangeOriginType.ThisRoom;
        }
    }

    /// <summary>Why a call with these keys is refused, null when they open the room's Web API.</summary>
    public async Task<string?> CheckWebApiKeysAsync(
        WiredWebApiOperation operation,
        string? readKey,
        string? writeKey,
        CancellationToken ct
    ) => (await OpenWebApiAsync(operation, readKey, writeKey, ct)).Refusal;

    /// <summary>
    /// The add-on whose keys a call needs, or why the call is refused. A room holding more than
    /// one add-on answers with the one placed first (lowest item id), so its keys are the same
    /// on every call (inference: Habbo's rule for a second add-on is not documented; its pickup
    /// text, "The Web API will stop working for this room", speaks of one per room).
    /// </summary>
    private async Task<(WiredAddonVariablesWebApi? Addon, string? Refusal)> OpenWebApiAsync(
        WiredWebApiOperation operation,
        string? readKey,
        string? writeKey,
        CancellationToken ct
    )
    {
        var addon = FurniModule
            .Items.Where(item => item.Logic is WiredAddonVariablesWebApi)
            .OrderBy(item => item.ObjectId.Value)
            .Select(item => (WiredAddonVariablesWebApi)item.Logic)
            .FirstOrDefault();

        if (addon is null)
            return (null, WiredWebApiErrors.API_DISABLED);

        await addon.EnsureWiredLoadedAsync(ct);

        var (reads, writes) = WiredWebApiKeys.KeysNeeded(operation);

        if (reads && KeyRefusal(readKey, addon.ReadKey) is { } readRefusal)
            return (addon, readRefusal);

        if (writes && KeyRefusal(writeKey, addon.WriteKey) is { } writeRefusal)
            return (addon, writeRefusal);

        return (addon, null);
    }

    private static string? KeyRefusal(string? sent, string key)
    {
        if (string.IsNullOrEmpty(sent))
            return WiredWebApiErrors.KEY_MISSING;

        return WiredWebApiKeys.Matches(sent, key) ? null : WiredWebApiErrors.KEY_INVALID;
    }

    private async Task<WiredWebApiResponse> DispatchWebApiAsync(
        WiredWebApiRequest request,
        WiredAddonVariablesWebApi addon,
        CancellationToken ct
    )
    {
        switch (request.Operation)
        {
            case WiredWebApiOperation.ListVariables:
                return WiredWebApiJson.Ok(
                    new JsonObject
                    {
                        ["users"] = NamesOf(WiredVariableTargetType.User),
                        ["furni"] = NamesOf(WiredVariableTargetType.Furni),
                        ["global"] = NamesOf(WiredVariableTargetType.Global),
                    }
                );
            case WiredWebApiOperation.GetValue:
            case WiredWebApiOperation.PutValue:
            case WiredWebApiOperation.PatchValue:
            case WiredWebApiOperation.DeleteValue:
            {
                if (!TryGetScopedVariable(request, out var variable, out var refusal))
                    return refusal!;

                return await RunValueOperationAsync(
                    variable!,
                    request.Operation,
                    request.TargetKind,
                    request.EntityId,
                    WiredWebApiJson.BodyMember(request.Body, "value"),
                    ct
                );
            }
            case WiredWebApiOperation.GetGlobal:
            case WiredWebApiOperation.PatchGlobal:
            {
                var variable = FindWebApiVariable(
                    request.VariableName,
                    WiredVariableTargetType.Global
                );

                if (variable is null)
                    return WiredWebApiJson.Error(404, WiredWebApiErrors.NOT_FOUND);

                return await RunValueOperationAsync(
                    variable,
                    request.Operation == WiredWebApiOperation.GetGlobal
                        ? WiredWebApiOperation.GetValue
                        : WiredWebApiOperation.PatchValue,
                    WiredWebApiEntity.GLOBAL,
                    "0",
                    WiredWebApiJson.BodyMember(request.Body, "value"),
                    ct
                );
            }
            case WiredWebApiOperation.ListValues:
            case WiredWebApiOperation.CountValues:
            {
                if (!TryGetScopedVariable(request, out var variable, out var refusal))
                    return refusal!;

                if (!WiredWebApiEntity.IsKindOf(request.TargetKind, ScopeTarget(request.Scope)))
                    return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

                return request.Operation == WiredWebApiOperation.CountValues
                    ? WiredWebApiJson.Ok(
                        new JsonObject { ["count"] = CountHolders(variable!, request.TargetKind) }
                    )
                    : ListHolders(variable!, request);
            }
            case WiredWebApiOperation.BulkDelete:
                return BulkDelete(addon, request.Body);
            case WiredWebApiOperation.Batch:
            {
                if (!TryGetScopedVariable(request, out var variable, out var refusal))
                    return refusal!;

                return await RunBatchAsync(variable!, request.Body, request.BatchWriteBudget, ct);
            }
            case WiredWebApiOperation.FindUserProfile:
                return FindUserProfile(request);
            case WiredWebApiOperation.GetUserProfile:
            case WiredWebApiOperation.GetFurniProfile:
            case WiredWebApiOperation.GetGlobalProfile:
            case WiredWebApiOperation.PatchUserProfile:
            case WiredWebApiOperation.PatchFurniProfile:
            case WiredWebApiOperation.PatchGlobalProfile:
            case WiredWebApiOperation.DeleteUserProfile:
                return await RunProfileOperationAsync(request, ct);
            default:
                return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);
        }
    }

    /// <summary>
    /// The room's variables the Web API knows: its variable boxes, by name and target. A pointer
    /// only has a target while a stack runs (<see cref="WiredVariableFlags.RequiresExecutionContext"/>),
    /// so it has none for the API and is not listed.
    /// </summary>
    private IEnumerable<FurnitureWiredVariableLogic> WebApiVariables(
        WiredVariableTargetType targetType
    ) =>
        GetAllVariables()
            .OfType<FurnitureWiredVariableLogic>()
            .Where(variable =>
                variable.GetVarSnapshot() is var snapshot
                && snapshot.TargetType == targetType
                && !snapshot.Flags.Has(WiredVariableFlags.RequiresExecutionContext)
            );

    /// <summary>
    /// What the Web API may do with a variable. A reference ("From Another Room") is read only
    /// through it: its values live in another room, which this room's keys do not open
    /// (inference: Habbo's API document says nothing of references).
    /// </summary>
    private static WiredVariableFlags WebApiFlags(FurnitureWiredVariableLogic variable)
    {
        var flags = variable.GetVarSnapshot().Flags;

        return variable is WiredVariableReference
            ? flags & ~(WiredVariableFlags.CanWriteValue | WiredVariableFlags.CanCreateAndDelete)
            : flags;
    }

    private JsonArray NamesOf(WiredVariableTargetType targetType) =>
        [
            .. WebApiVariables(targetType)
                .Select(variable => variable.GetVarSnapshot().VariableName)
                .Distinct(StringComparer.Ordinal)
                .Select(name => (JsonNode?)JsonValue.Create(name)),
        ];

    private FurnitureWiredVariableLogic? FindWebApiVariable(
        string name,
        WiredVariableTargetType targetType
    ) =>
        WebApiVariables(targetType)
            .FirstOrDefault(variable =>
                string.Equals(
                    variable.GetVarSnapshot().VariableName,
                    name,
                    StringComparison.Ordinal
                )
            );

    private static WiredVariableTargetType ScopeTarget(string scope) =>
        scope switch
        {
            "user" => WiredVariableTargetType.User,
            "furni" => WiredVariableTargetType.Furni,
            _ => WiredVariableTargetType.None,
        };

    private bool TryGetScopedVariable(
        WiredWebApiRequest request,
        out FurnitureWiredVariableLogic? variable,
        out WiredWebApiResponse? refusal
    )
    {
        variable = null;
        refusal = null;

        var targetType = ScopeTarget(request.Scope);

        if (targetType == WiredVariableTargetType.None)
        {
            refusal = WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

            return false;
        }

        variable = FindWebApiVariable(request.VariableName, targetType);

        if (variable is null)
        {
            refusal = WiredWebApiJson.Error(404, WiredWebApiErrors.NOT_FOUND);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Who or what an entity id of a target kind names in this room. Wall items go by their id
    /// (Habbo's client calls them <c>-id</c>, the API by the positive id), Builders Club furni by
    /// their id less <c>0x7FFFFFFF - 0xFFFF</c> (<see cref="FurniIdBands.BuildersClubMin"/>), a
    /// player by player id, a pet by pet id and a bot by bot id. Null when the id cannot name
    /// anything of that kind; an entity that is not here and holds nothing is <c>Absent</c>.
    /// </summary>
    private WiredWebApiEntity? ResolveEntity(string targetKind, string entityId)
    {
        if (targetKind == WiredWebApiEntity.GLOBAL)
            return new WiredWebApiEntity(WiredVariableTargetType.Global, targetKind, 0, 0, null);

        if (!int.TryParse(entityId, out var id) || id < 0)
            return null;

        switch (targetKind)
        {
            case WiredWebApiEntity.USERS:
                return AvatarModule.TryGetPlayer(new PlayerId(id), out var player)
                    ? new WiredWebApiEntity(
                        WiredVariableTargetType.User,
                        targetKind,
                        id,
                        player.ObjectId.Value,
                        player.Name
                    )
                    : new WiredWebApiEntity(WiredVariableTargetType.User, targetKind, id, -1, null);
            case WiredWebApiEntity.PETS:
            {
                var pet = AvatarModule
                    .Avatars.OfType<IRoomPet>()
                    .FirstOrDefault(x => x.PetId == id);

                return new WiredWebApiEntity(
                    WiredVariableTargetType.User,
                    targetKind,
                    id,
                    pet?.ObjectId.Value ?? -1,
                    pet?.Name
                );
            }
            case WiredWebApiEntity.BOTS:
            {
                var bot = AvatarModule
                    .Avatars.OfType<IRoomBot>()
                    .FirstOrDefault(x => x.BotId == id);

                return new WiredWebApiEntity(
                    WiredVariableTargetType.User,
                    targetKind,
                    id,
                    bot?.ObjectId.Value ?? -1,
                    bot?.Name
                );
            }
            case WiredWebApiEntity.FURNI:
            case WiredWebApiEntity.FURNI_BC:
            case WiredWebApiEntity.WALL_ITEMS:
            case WiredWebApiEntity.WALL_ITEMS_BC:
            {
                var buildersClub = targetKind.EndsWith("-bc", StringComparison.Ordinal);

                if (buildersClub && id >= FurniIdBands.BuildersClubCapacity)
                    return null;

                var objectId = buildersClub ? FurniIdBands.BuildersClubMin + id : id;
                var live =
                    FurniModule.TryGetItem(RoomObjectId.Parse(objectId), out var item)
                    && WiredWebApiEntity.KindOf(item) == targetKind;

                return new WiredWebApiEntity(
                    WiredVariableTargetType.Furni,
                    targetKind,
                    id,
                    live ? objectId : -1,
                    null
                );
            }
            default:
                return null;
        }
    }

    private bool TryReadWebApiValue(
        FurnitureWiredVariableLogic variable,
        WiredWebApiEntity entity,
        out WiredVariableValue value,
        out long createdAtMs,
        out long updatedAtMs
    )
    {
        createdAtMs = 0;
        updatedAtMs = 0;

        value = default;

        if (!entity.IsHere)
            return entity.Kind == WiredWebApiEntity.USERS
                && variable.TryGetAbsentPlayerValue(
                    entity.ApiId,
                    out value,
                    out createdAtMs,
                    out updatedAtMs
                );

        var key = entity.KeyFor(variable);

        if (!variable.TryGetValue(key, out value))
            return false;

        variable.TryGetTimestamps(key, out createdAtMs, out updatedAtMs);

        return true;
    }

    /// <summary>
    /// One value read or written. PUT gives the variable (replacing a value it held) and needs a
    /// variable that can be given and taken; PATCH changes a held value of a variable with a
    /// writable value; DELETE takes the variable away. A player who is neither here nor holding
    /// anything permanent in the room is not part of it: <c>user_not_participating</c>.
    /// </summary>
    private async Task<WiredWebApiResponse> RunValueOperationAsync(
        FurnitureWiredVariableLogic variable,
        WiredWebApiOperation operation,
        string targetKind,
        string entityId,
        JsonNode? valueNode,
        CancellationToken ct
    )
    {
        var targetType = variable.GetVarSnapshot().TargetType;

        if (!WiredWebApiEntity.IsKindOf(targetKind, targetType))
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

        if (ResolveEntity(targetKind, entityId) is not { } entity)
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

        var flags = WebApiFlags(variable);
        var outcome = WiredWebApiErrors.NOT_FOUND;

        switch (operation)
        {
            case WiredWebApiOperation.GetValue:
                break;
            case WiredWebApiOperation.PutValue:
            case WiredWebApiOperation.PatchValue:
            {
                if (!WiredWebApiJson.TryReadValue(valueNode, out var value))
                    return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_VALUE);

                outcome = await WriteWebApiValueAsync(
                    variable,
                    entity,
                    value,
                    operation == WiredWebApiOperation.PutValue,
                    ct
                );

                if (outcome is not null)
                    return WiredWebApiJson.Error(WiredWebApiErrors.StatusOf(outcome), outcome);

                break;
            }
            case WiredWebApiOperation.DeleteValue:
                outcome = RemoveWebApiValue(variable, entity, flags);

                return outcome is null
                    ? WiredWebApiJson.NoContent
                    : WiredWebApiJson.Error(WiredWebApiErrors.StatusOf(outcome), outcome);
        }

        return TryReadWebApiValue(variable, entity, out var held, out var created, out var updated)
            ? WiredWebApiJson.Ok(WiredWebApiJson.Variable(held, created, updated))
            : WiredWebApiJson.Error(404, WiredWebApiErrors.NOT_FOUND);
    }

    /// <summary>Null when written, else the error code that says why not.</summary>
    /// <summary>
    /// Why writing (or, with <paramref name="give"/>, giving) <paramref name="variable"/> to
    /// <paramref name="entity"/> is refused, null when it may be: everything that can be known
    /// before the write is made.
    /// </summary>
    private string? WebApiWriteRefusal(
        FurnitureWiredVariableLogic variable,
        WiredWebApiEntity entity,
        bool give
    )
    {
        var flags = WebApiFlags(variable);

        if (give && !flags.Has(WiredVariableFlags.CanCreateAndDelete))
            return WiredWebApiErrors.OPERATION_NOT_ALLOWED;

        if (
            !give
            && (
                !flags.Has(WiredVariableFlags.HasValue)
                || !flags.Has(WiredVariableFlags.CanWriteValue)
            )
        )
            return WiredWebApiErrors.OPERATION_NOT_ALLOWED;

        if (!entity.IsHere)
        {
            if (entity.Kind != WiredWebApiEntity.USERS || !HasUserProfile(entity.ApiId))
                return entity.Kind == WiredWebApiEntity.USERS
                    ? WiredWebApiErrors.USER_NOT_PARTICIPATING
                    : WiredWebApiErrors.NOT_FOUND;

            if (!variable.KeepsAbsentPlayers)
                return WiredWebApiErrors.USER_NOT_PARTICIPATING;

            if (!give && !variable.TryGetAbsentPlayerValue(entity.ApiId, out _, out _, out _))
                return WiredWebApiErrors.NOT_FOUND;

            return null;
        }

        if (!give && !variable.TryGetValue(entity.KeyFor(variable), out _))
            return WiredWebApiErrors.NOT_FOUND;

        return null;
    }

    private async Task<string?> WriteWebApiValueAsync(
        FurnitureWiredVariableLogic variable,
        WiredWebApiEntity entity,
        WiredVariableValue value,
        bool give,
        CancellationToken ct
    )
    {
        if (WebApiWriteRefusal(variable, entity, give) is { } refusal)
            return refusal;

        // A variable without a value is only held or not; the number sent is not kept.
        var written = WebApiFlags(variable).Has(WiredVariableFlags.HasValue)
            ? value
            : WiredVariableValue.Default;
        var ctx = new WiredExecutionContext(_roomGrain) { CancellationToken = ct };

        if (!entity.IsHere)
            return await variable.WriteAbsentPlayerValueAsync(entity.ApiId, written, give, ctx)
                ? null
                : WiredWebApiErrors.OPERATION_NOT_ALLOWED;

        var key = entity.KeyFor(variable);

        if (give)
            return await variable.GiveValueAsync(key, written, replace: true)
                ? null
                : WiredWebApiErrors.OPERATION_NOT_ALLOWED;

        if (!await variable.SetValueAsync(ctx, key, written))
            return WiredWebApiErrors.OPERATION_NOT_ALLOWED;

        FlushWiredContext(ctx);

        return null;
    }

    private string? RemoveWebApiValue(
        FurnitureWiredVariableLogic variable,
        WiredWebApiEntity entity,
        WiredVariableFlags flags
    )
    {
        if (!flags.Has(WiredVariableFlags.CanCreateAndDelete))
            return WiredWebApiErrors.OPERATION_NOT_ALLOWED;

        if (!entity.IsHere)
            return
                entity.Kind == WiredWebApiEntity.USERS
                && variable.RemoveAbsentPlayerValue(entity.ApiId)
                ? null
                : WiredWebApiErrors.NOT_FOUND;

        return variable.RemoveValue(entity.KeyFor(variable)) ? null : WiredWebApiErrors.NOT_FOUND;
    }

    /// <summary>Whether an absent player holds anything permanent in this room.</summary>
    private bool HasUserProfile(int playerId) =>
        WebApiVariables(WiredVariableTargetType.User)
            .Any(variable => variable.TryGetAbsentPlayerValue(playerId, out _, out _, out _));

    /// <summary>Every holder of a variable among the entities of one target kind.</summary>
    private IEnumerable<(
        int ApiId,
        WiredVariableValue Value,
        long CreatedAtMs,
        long UpdatedAtMs
    )> HoldersOf(FurnitureWiredVariableLogic variable, string targetKind)
    {
        var snapshot = variable.GetVarSnapshot();
        var here = new HashSet<int>();

        if (snapshot.TargetType == WiredVariableTargetType.User)
        {
            foreach (var avatar in AvatarModule.Avatars)
            {
                var apiId = avatar switch
                {
                    IRoomPlayer player when targetKind == WiredWebApiEntity.USERS => player
                        .PlayerId
                        .Value,
                    IRoomPet pet when targetKind == WiredWebApiEntity.PETS => pet.PetId,
                    IRoomBot bot when targetKind == WiredWebApiEntity.BOTS => bot.BotId,
                    _ => -1,
                };

                if (apiId < 0)
                    continue;

                here.Add(apiId);

                var key = new WiredVariableKey(
                    snapshot.VariableId,
                    WiredVariableTargetType.User,
                    avatar.ObjectId.Value
                );

                if (!variable.TryGetValue(key, out var value))
                    continue;

                variable.TryGetTimestamps(key, out var created, out var updated);

                yield return (apiId, value, created, updated);
            }

            if (targetKind == WiredWebApiEntity.USERS && variable.KeepsAbsentPlayers)
            {
                foreach (var (playerId, value, created, updated) in variable.GetStoredUserValues())
                {
                    if (!here.Contains(playerId))
                        yield return (playerId, value, created, updated);
                }
            }

            yield break;
        }

        foreach (var item in FurniModule.Items)
        {
            if (WiredWebApiEntity.KindOf(item) != targetKind)
                continue;

            var key = new WiredVariableKey(
                snapshot.VariableId,
                WiredVariableTargetType.Furni,
                item.ObjectId.Value
            );

            if (!variable.TryGetValue(key, out var value))
                continue;

            variable.TryGetTimestamps(key, out var created, out var updated);

            yield return (WiredWebApiEntity.ApiIdOf(item), value, created, updated);
        }
    }

    /// <summary>
    /// The number of holders, cached as Habbo does: 20 s under 1,000, 60 s under 10,000, 300 s
    /// under 100,000, 600 s above (The Wired Faculty, #api-technical-info).
    /// </summary>
    private int CountHolders(FurnitureWiredVariableLogic variable, string targetKind)
    {
        var now = NowMs();
        var cacheKey = (variable.GetVarSnapshot().VariableId, targetKind);

        if (_webApiCounts.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtMs > now)
            return cached.Count;

        var count = HoldersOf(variable, targetKind).Count();
        var seconds = count switch
        {
            < 1_000 => 20,
            < 10_000 => 60,
            < 100_000 => 300,
            _ => 600,
        };

        _webApiCounts[cacheKey] = (count, now + seconds * 1000L);

        return count;
    }

    /// <summary>
    /// One page of holders (page from 1, size 50 by default and at most 100). The items carry
    /// the entity's id beside the value and its times (inference: the document leaves the item
    /// shape open); ordered by creation time unless <c>order_by</c> says otherwise. A page below
    /// 1 is refused: Habbo answered page 0 with a 400. Only the status was observed; the error
    /// code is the document's code for a bad value (inference). So is a size outside 1-100 and
    /// an <c>order_by</c> or <c>order_dir</c> the document does not list (inference: only page 0
    /// was tried on Habbo).
    /// </summary>
    private WiredWebApiResponse ListHolders(
        FurnitureWiredVariableLogic variable,
        WiredWebApiRequest request
    )
    {
        var page = request.Page ?? 1;

        var size = request.Size ?? DEFAULT_PAGE_SIZE;

        if (
            page < 1
            || size is < 1 or > MAX_PAGE_SIZE
            || request.OrderBy is not (null or "value" or "creation_time" or "update_time")
            || request.OrderDir is not (null or "asc" or "desc")
        )
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_VALUE);

        var descending = request.OrderDir == "desc";
        var holders = HoldersOf(variable, request.TargetKind);
        Func<
            (int ApiId, WiredVariableValue Value, long CreatedAtMs, long UpdatedAtMs),
            long
        > order = request.OrderBy switch
        {
            "value" => holder => holder.Value.Value,
            "update_time" => holder => holder.UpdatedAtMs,
            _ => holder => holder.CreatedAtMs,
        };
        var ordered = descending
            ? holders.OrderByDescending(order).ThenBy(holder => holder.ApiId)
            : holders.OrderBy(order).ThenBy(holder => holder.ApiId);
        var items = new JsonArray();

        foreach (var holder in ordered.Skip((page - 1) * size).Take(size))
        {
            var item = WiredWebApiJson.Variable(
                holder.Value,
                holder.CreatedAtMs,
                holder.UpdatedAtMs
            );

            item.Insert(0, "id", holder.ApiId);
            items.Add(item);
        }

        return WiredWebApiJson.Ok(
            new JsonObject
            {
                ["items"] = items,
                ["page"] = page,
                ["size"] = size,
            }
        );
    }

    /// <summary>
    /// Takes every stored value of the named user and furni variables, from users, pets, bots,
    /// furni and wall items alike; the boxes stay. Only with "Allow mass deletion" ticked.
    /// </summary>
    private WiredWebApiResponse BulkDelete(WiredAddonVariablesWebApi addon, string? body)
    {
        if (!addon.AllowsBulkDelete)
            return WiredWebApiJson.Error(403, WiredWebApiErrors.BULK_DELETE_NOT_ENABLED);

        var names = WiredWebApiJson.BodyMember(body, "variables") as JsonArray;

        if (names is null || names.Count == 0)
            return WiredWebApiJson.Error(400, WiredWebApiErrors.BULK_DELETE_EMPTY);

        var variables = new List<FurnitureWiredVariableLogic>();

        foreach (var node in names)
        {
            var name =
                node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var variable = name is null
                ? null
                : FindWebApiVariable(name, WiredVariableTargetType.User)
                    ?? FindWebApiVariable(name, WiredVariableTargetType.Furni);

            if (
                variable is null
                || !WebApiFlags(variable).Has(WiredVariableFlags.CanCreateAndDelete)
            )
                return WiredWebApiJson.Error(400, WiredWebApiErrors.BULK_DELETE_INVALID_VARIABLE);

            variables.Add(variable);
        }

        foreach (var variable in variables)
        {
            var snapshot = variable.GetVarSnapshot();

            foreach (var targetId in GetLiveTargetIds(snapshot.TargetType).ToList())
                variable.RemoveValue(
                    new WiredVariableKey(snapshot.VariableId, snapshot.TargetType, targetId)
                );

            variable.RemoveAllValues();
        }

        _webApiCounts.Clear();

        return WiredWebApiJson.NoContent;
    }

    /// <summary>
    /// Up to 50 reads and writes of one variable, each answered in its own result with the
    /// <c>op_id</c> it came with; one failing does not stop the rest. Each write is paid from the
    /// room's batched write budget, and one the budget cannot pay is answered 429 on its own
    /// (<c>WiredBatchOperationResultError</c> allows a 429 per operation).
    /// </summary>
    private async Task<WiredWebApiResponse> RunBatchAsync(
        FurnitureWiredVariableLogic variable,
        string? body,
        int writeBudget,
        CancellationToken ct
    )
    {
        var requests = WiredWebApiJson.BodyMember(body, "requests") as JsonArray;

        if (requests is null || requests.Count == 0)
            return WiredWebApiJson.Error(400, WiredWebApiErrors.BATCH_EMPTY);

        if (requests.Count > MAX_BATCH_OPERATIONS)
            return WiredWebApiJson.Error(400, WiredWebApiErrors.BATCH_LIMIT_EXCEEDED);

        var results = new JsonArray();

        foreach (var node in requests)
        {
            var opId = WiredWebApiJson.StringOf(WiredWebApiJson.Member(node, "op_id"));
            var operation = WiredWebApiJson.StringOf(WiredWebApiJson.Member(node, "method")) switch
            {
                "GET" => WiredWebApiOperation.GetValue,
                "PUT" => WiredWebApiOperation.PutValue,
                "PATCH" => WiredWebApiOperation.PatchValue,
                "DELETE" => WiredWebApiOperation.DeleteValue,
                _ => (WiredWebApiOperation?)null,
            };
            var path = (
                WiredWebApiJson.StringOf(WiredWebApiJson.Member(node, "path")) ?? string.Empty
            ).Split('/');
            var response =
                operation is null || path.Length != 2
                    ? WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET)
                : operation != WiredWebApiOperation.GetValue
                && !TrySpendWebApiWrites(writeBudget, 1)
                    ? WiredWebApiJson.Error(429, WiredWebApiErrors.TOO_MANY_REQUESTS)
                : await RunValueOperationAsync(
                    variable,
                    operation.Value,
                    path[0],
                    path[1],
                    WiredWebApiJson.Member(WiredWebApiJson.Member(node, "body"), "value"),
                    ct
                );

            results.Add(WiredWebApiJson.BatchResult(opId, response));
        }

        return WiredWebApiJson.Ok(new JsonObject { ["results"] = results });
    }

    /// <summary>
    /// Pays writes from the room's batched write budget, writes a minute on top of the batch
    /// call limit ("extra batched write-cost budget 500/min", The Wired Faculty,
    /// #api-technical-info): all of them or none. A budget of 0 pays everything.
    /// </summary>
    private bool TrySpendWebApiWrites(int budget, int writes)
    {
        if (budget <= 0)
            return true;

        var now = NowMs();

        if (now - _webApiBudgetSinceMs >= 60_000)
        {
            _webApiBudgetSinceMs = now;
            _webApiBudgetSpent = 0;
        }

        if (_webApiBudgetSpent + writes > budget)
            return false;

        _webApiBudgetSpent += writes;

        return true;
    }

    /// <summary>
    /// A player's profile by name or unique id, whether they are here or away holding a
    /// permanent variable; the hotel names who the call means (<see cref="WiredWebApiRequest.PlayerId"/>).
    /// </summary>
    private WiredWebApiResponse FindUserProfile(WiredWebApiRequest request)
    {
        if (string.IsNullOrEmpty(request.Name) && string.IsNullOrEmpty(request.UniqueId))
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

        var here = string.IsNullOrEmpty(request.Name)
            ? null
            : AvatarModule.Players.FirstOrDefault(x =>
                string.Equals(x.Name, request.Name, StringComparison.OrdinalIgnoreCase)
            );
        var playerId = here?.PlayerId.Value ?? request.PlayerId;

        if (
            playerId is null
            || ResolveEntity(WiredWebApiEntity.USERS, playerId.Value.ToString()) is not { } entity
            || (!entity.IsHere && !HasUserProfile(entity.ApiId))
        )
            return WiredWebApiJson.Error(404, WiredWebApiErrors.ENTITY_NOT_FOUND);

        return WiredWebApiJson.Ok(
            ProfileOf(entity with { Name = entity.Name ?? request.PlayerName })
        );
    }

    /// <summary>
    /// A variables profile: every variable an entity holds, by name. A furni's exists while it is
    /// in the room, the room's always, a user's, pet's or bot's while it is here or (a player)
    /// holds a permanent variable. PATCH writes several at once (null takes one away), DELETE
    /// takes them all.
    /// </summary>
    private async Task<WiredWebApiResponse> RunProfileOperationAsync(
        WiredWebApiRequest request,
        CancellationToken ct
    )
    {
        var global =
            request.Operation
            is WiredWebApiOperation.GetGlobalProfile
                or WiredWebApiOperation.PatchGlobalProfile;
        var targetType =
            global ? WiredVariableTargetType.Global
            : request.Operation
                is WiredWebApiOperation.GetFurniProfile
                    or WiredWebApiOperation.PatchFurniProfile
                ? WiredVariableTargetType.Furni
            : WiredVariableTargetType.User;
        var kind = global ? WiredWebApiEntity.GLOBAL : request.TargetKind;

        if (!global && !WiredWebApiEntity.IsKindOf(kind, targetType))
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

        if (ResolveEntity(kind, request.EntityId) is not { } entity)
            return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_TARGET);

        if (
            entity.Name is null
            && kind == WiredWebApiEntity.USERS
            && request.PlayerId == entity.ApiId
        )
            entity = entity with { Name = request.PlayerName };

        var exists =
            entity.IsHere
            || (entity.Kind == WiredWebApiEntity.USERS && HasUserProfile(entity.ApiId));

        if (!exists)
            return WiredWebApiJson.Error(404, WiredWebApiErrors.ENTITY_NOT_FOUND);

        switch (request.Operation)
        {
            case WiredWebApiOperation.DeleteUserProfile:
            {
                // All or nothing: a variable that may not be taken away refuses the whole call.
                var held = WebApiVariables(targetType)
                    .Where(variable => TryReadWebApiValue(variable, entity, out _, out _, out _))
                    .ToList();

                if (
                    held.Any(variable =>
                        !WebApiFlags(variable).Has(WiredVariableFlags.CanCreateAndDelete)
                    )
                )
                    return WiredWebApiJson.Error(403, WiredWebApiErrors.OPERATION_NOT_ALLOWED);

                foreach (var variable in held)
                {
                    if (RemoveWebApiValue(variable, entity, WebApiFlags(variable)) is { } outcome)
                        return WiredWebApiJson.Error(WiredWebApiErrors.StatusOf(outcome), outcome);
                }

                return WiredWebApiJson.NoContent;
            }
            case WiredWebApiOperation.PatchUserProfile:
            case WiredWebApiOperation.PatchFurniProfile:
            case WiredWebApiOperation.PatchGlobalProfile:
            {
                if (WiredWebApiJson.BodyMember(request.Body, "variables") is not JsonObject changes)
                    return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_VALUE);

                var planned =
                    new List<(
                        FurnitureWiredVariableLogic Variable,
                        WiredVariableValue? Value,
                        bool Give
                    )>();

                foreach (var (name, node) in changes)
                {
                    var variable = FindWebApiVariable(name, targetType);

                    if (variable is null)
                        return WiredWebApiJson.Error(404, WiredWebApiErrors.NOT_FOUND);

                    var held = TryReadWebApiValue(variable, entity, out _, out _, out _);

                    if (node is null)
                    {
                        if (
                            global
                            || (
                                held
                                && !WebApiFlags(variable).Has(WiredVariableFlags.CanCreateAndDelete)
                            )
                        )
                            return WiredWebApiJson.Error(
                                403,
                                WiredWebApiErrors.OPERATION_NOT_ALLOWED
                            );

                        planned.Add((variable, null, false));

                        continue;
                    }

                    if (!WiredWebApiJson.TryReadValue(node, out var value))
                        return WiredWebApiJson.Error(400, WiredWebApiErrors.INVALID_VALUE);

                    var give = !global && !held;

                    // Every change is checked before any is made, so a refused one leaves the
                    // profile as it was.
                    if (WebApiWriteRefusal(variable, entity, give) is { } refusal)
                        return WiredWebApiJson.Error(WiredWebApiErrors.StatusOf(refusal), refusal);

                    planned.Add((variable, value, give));
                }

                // Habbo counts a profile PATCH as a batch: each change is a batched write.
                if (!TrySpendWebApiWrites(request.BatchWriteBudget, planned.Count))
                    return WiredWebApiJson.Error(429, WiredWebApiErrors.TOO_MANY_REQUESTS);

                foreach (var (variable, value, give) in planned)
                {
                    string? outcome;

                    if (value is null)
                        outcome = TryReadWebApiValue(variable, entity, out _, out _, out _)
                            ? RemoveWebApiValue(variable, entity, WebApiFlags(variable))
                            : null;
                    else
                        outcome = await WriteWebApiValueAsync(
                            variable,
                            entity,
                            value.Value,
                            give,
                            ct
                        );

                    if (outcome is not null)
                        return WiredWebApiJson.Error(WiredWebApiErrors.StatusOf(outcome), outcome);
                }

                break;
            }
        }

        return WiredWebApiJson.Ok(ProfileOf(entity));
    }

    private JsonObject ProfileOf(WiredWebApiEntity entity)
    {
        var variables = new JsonObject();

        foreach (var variable in WebApiVariables(entity.TargetType))
        {
            var name = variable.GetVarSnapshot().VariableName;

            if (
                !variables.ContainsKey(name)
                && TryReadWebApiValue(
                    variable,
                    entity,
                    out var value,
                    out var created,
                    out var updated
                )
            )
                variables[name] = WiredWebApiJson.Variable(value, created, updated);
        }

        var profile = new JsonObject { ["variables"] = variables };

        if (entity.TargetType == WiredVariableTargetType.Global)
            return profile;

        var target = new JsonObject { ["id"] = entity.ApiId };

        if (entity.Name is not null)
            target["name"] = entity.Name;

        // WiredUserProfileTarget.unique_id: this hotel has no unique ids, so a player's is their
        // player id, the one users?unique_id= finds them by (inference).
        if (entity.Kind == WiredWebApiEntity.USERS)
            target["unique_id"] = entity.ApiId.ToString(CultureInfo.InvariantCulture);

        profile[WiredWebApiEntity.ProfileFieldOf(entity.Kind)] = target;

        return profile;
    }
}
