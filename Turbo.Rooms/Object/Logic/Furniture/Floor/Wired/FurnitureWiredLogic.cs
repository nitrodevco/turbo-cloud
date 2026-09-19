using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

public abstract partial class FurnitureWiredLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx), IWiredBox
{
    protected readonly IGrainFactory _grainFactory = grainFactory;

    public abstract WiredType WiredType { get; }
    public abstract int WiredCode { get; }

    protected override StuffPersistanceType _stuffPersistanceType =>
        StuffPersistanceType.RoomActive;

    protected IWiredData _wiredData = null!;

    public RoomObjectId ObjectId => _ctx.ObjectId;

    private WiredDataSnapshot? _snapshot;

    public async Task LoadWiredAsync(CancellationToken ct)
    {
        await FillInternalDataAsync(ct);
    }

    public Task FlashActivationStateAsync(CancellationToken ct) =>
        SetStateAsync(GetState() == 1 ? 0 : 1);

    public virtual List<int> GetStuffIds()
    {
        if (GetValidStuffIds(_wiredData.StuffIds, out var stuffIds))
        {
            if (!_wiredData.StuffIds.SequenceEqual(stuffIds))
            {
                _wiredData.StuffIds = stuffIds;

                _wiredData.MarkDirty();
            }
        }

        return stuffIds ?? [];
    }

    public virtual List<int> GetStuffIds2()
    {
        if (GetValidStuffIds(_wiredData.StuffIds2, out var stuffIds))
        {
            if (!_wiredData.StuffIds2.SequenceEqual(stuffIds))
            {
                _wiredData.StuffIds2 = stuffIds;

                _wiredData.MarkDirty();
            }
        }

        return stuffIds ?? [];
    }

    public virtual List<IWiredParamRule> GetIntParamRules() => [];

    public virtual IWiredParamRule? GetIntParamTailRule() => null;

    public virtual int GetMaxVariableIds() => 0;

    public virtual List<WiredFurniSourceType[]> GetAllowedFurniSources() => [];

    public virtual List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [];

    public virtual List<Type> GetDefinitionSpecificTypes() => [];

    public virtual List<Type> GetTypeSpecificTypes() => [];

    public virtual bool SupportsAdvancedMode() => true;

    public virtual List<WiredVariableContextSnapshot> GetWiredContextSnapshots() => [];

    public List<WiredFurniSourceType[]> GetFurniSources()
    {
        var sources = new List<WiredFurniSourceType[]>();
        var index = 0;

        foreach (var source in GetDefaultFurniSources())
        {
            WiredFurniSourceType[] sourceTypes = source;

            try
            {
                if (_wiredData.FurniSources[index] is not null)
                {
                    sourceTypes = _wiredData.FurniSources[index];
                }
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);
            }

            sources.Add(sourceTypes);
            index++;
        }

        return sources;
    }

    public List<WiredPlayerSourceType[]> GetPlayerSources()
    {
        var sources = new List<WiredPlayerSourceType[]>();
        var index = 0;

        foreach (var source in GetDefaultPlayerSources())
        {
            WiredPlayerSourceType[] sourceTypes = source;

            try
            {
                if (_wiredData.PlayerSources[index] is not null)
                {
                    sourceTypes = _wiredData.PlayerSources[index];
                }
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);
            }

            sources.Add(sourceTypes);
            index++;
        }

        return sources;
    }

    public List<WiredFurniSourceType[]> GetDefaultFurniSources() =>
        [.. GetAllowedFurniSources().Select(x => new[] { x[0] })];

    public List<WiredPlayerSourceType[]> GetDefaultPlayerSources() =>
        [.. GetAllowedPlayerSources().Select(x => new[] { x[0] })];

    public List<object> GetDefinitionSpecifics()
    {
        var specifics = new List<object>();
        var index = 0;

        foreach (var specType in GetDefinitionSpecificTypes())
        {
            object specific = null!;

            try
            {
                if (
                    _wiredData.DefinitionSpecifics[index] is not null
                    && specType.IsAssignableFrom(_wiredData.DefinitionSpecifics[index].GetType())
                )
                {
                    specific = _wiredData.DefinitionSpecifics[index];
                }
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);
            }

            specific ??= Activator.CreateInstance(specType)!;

            specifics.Add(specific);
            index++;
        }

        return specifics;
    }

    public List<object> GetTypeSpecifics()
    {
        var specifics = new List<object>();
        var index = 0;

        foreach (var specType in GetTypeSpecificTypes())
        {
            object specific = null!;

            try
            {
                if (
                    _wiredData.TypeSpecifics[index] is not null
                    && specType.IsAssignableFrom(_wiredData.TypeSpecifics[index].GetType())
                )
                {
                    specific = _wiredData.TypeSpecifics[index];
                }
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);
            }

            specific ??= Activator.CreateInstance(specType)!;

            specifics.Add(specific);
            index++;
        }

        return specifics;
    }

    public List<int> GetDefaultIntParams()
    {
        var ints = new List<int>();

        foreach (var rule in GetIntParamRules())
            ints.Add(rule.DefaultValue);

        return ints;
    }

    public virtual async Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        try
        {
            var intParams = new List<int>();
            var stringParam = update.StringParam;
            var stuffIds = new List<int>();
            var stuffIds2 = new List<int>();
            var variableIds = new List<string>();
            var furniSources = new List<WiredFurniSourceType[]>();
            var playerSources = new List<WiredPlayerSourceType[]>();
            var definitionSpecifics = new List<object>();
            var typeSpecifics = new List<object>();

            if (TryNormalizeIntParams(update.IntParams, out var normalizedIntParams))
            {
                intParams = normalizedIntParams;
            }
            else
            {
                return false;
            }

            if (GetValidStuffIds(update.StuffIds, out var validStuffIds))
                stuffIds = validStuffIds;

            if (GetValidStuffIds(update.StuffIds2, out var validStuffIds2))
                stuffIds2 = validStuffIds2;

            if (GetValidVariableIds(update.VariableIds, out var validVariableIds))
                variableIds = [.. validVariableIds.Select(x => x.ToString())];

            var index = 0;
            var validFurniSources = GetAllowedFurniSources();

            foreach (var source in GetDefaultFurniSources())
            {
                WiredFurniSourceType[]? sourceTypes = source;

                try
                {
                    if (update.FurniSources[index] is not null)
                    {
                        sourceTypes =
                        [
                            .. update
                                .FurniSources[index]
                                .Where(validFurniSources[index].Contains)
                                .Take(source.Length),
                        ];
                    }
                }
                catch (Exception ex)
                {
                    LogWiredDataFault(ex);
                }

                furniSources.Add(sourceTypes);
                index++;
            }

            index = 0;
            var validPlayerSources = GetAllowedPlayerSources();

            foreach (var source in GetDefaultPlayerSources())
            {
                WiredPlayerSourceType[]? sourceTypes = source;

                try
                {
                    if (update.PlayerSources[index] is not null)
                    {
                        sourceTypes =
                        [
                            .. update
                                .PlayerSources[index]
                                .Where(validPlayerSources[index].Contains)
                                .Take(source.Length),
                        ];
                    }
                }
                catch (Exception ex)
                {
                    LogWiredDataFault(ex);
                }

                playerSources.Add(sourceTypes);
                index++;
            }

            index = 0;

            foreach (var specType in GetDefinitionSpecificTypes())
            {
                object specific = default!;

                try
                {
                    if (
                        update.DefinitionSpecifics[index] is not null
                        && specType.IsAssignableFrom(update.DefinitionSpecifics[index].GetType())
                    )
                    {
                        specific = update.DefinitionSpecifics[index];
                    }
                    else
                    {
                        specific = Activator.CreateInstance(specType)!;
                    }
                }
                catch (Exception ex)
                {
                    LogWiredDataFault(ex);
                }

                definitionSpecifics.Add(specific);
                index++;
            }

            index = 0;

            foreach (var specType in GetTypeSpecificTypes())
            {
                object specific = default!;

                try
                {
                    if (
                        update.TypeSpecifics[index] is not null
                        && specType.IsAssignableFrom(update.TypeSpecifics[index].GetType())
                    )
                    {
                        specific = update.TypeSpecifics[index];
                    }
                    else
                    {
                        specific = Activator.CreateInstance(specType)!;
                    }
                }
                catch (Exception ex)
                {
                    LogWiredDataFault(ex);
                }

                typeSpecifics.Add(specific);
                index++;
            }

            _wiredData.IntParams = intParams;
            _wiredData.StringParam = stringParam;
            _wiredData.StuffIds = stuffIds;
            _wiredData.StuffIds2 = stuffIds2;
            _wiredData.VariableIds = variableIds;
            _wiredData.FurniSources = furniSources;
            _wiredData.PlayerSources = playerSources;
            _wiredData.DefinitionSpecifics = definitionSpecifics;
            _wiredData.TypeSpecifics = typeSpecifics;

            _wiredData.MarkDirty();

            await OnWiredStackChangedAsync(ctx, [_ctx.GetTileIdx()], ct);

            return true;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Failed to apply a wired update to item {ItemId} in room {RoomId} for player {PlayerId}",
                _ctx.ObjectId,
                _ctx.RoomId,
                ctx.PlayerId
            );

            return false;
        }
    }

    protected virtual bool TryNormalizeIntParams(List<int> proposed, out List<int> normalized)
    {
        normalized = [];

        var fixedRules = GetIntParamRules();
        var tailRule = GetIntParamTailRule();
        var min = fixedRules.Count;
        var max = Math.Max(min, _roomGrain._roomConfig.WiredMaxIntParams);

        if (proposed.Count > max)
            return false;

        if (tailRule is null)
        {
            if (proposed.Count != fixedRules.Count)
                return false;

            for (var i = 0; i < fixedRules.Count; i++)
            {
                var rule = fixedRules[i];

                try
                {
                    var v = proposed[i];

                    if (!rule.IsValid(v))
                        return false;

                    normalized.Add(rule.Sanitize(v));
                }
                catch (Exception ex)
                {
                    LogWiredDataFault(ex);
                    normalized.Add(fixedRules[i].DefaultValue);
                }
            }

            return true;
        }

        if (proposed.Count < min)
            return false;

        for (int i = 0; i < fixedRules.Count; i++)
        {
            var rule = fixedRules[i];
            var v = i < proposed.Count ? proposed[i] : rule.DefaultValue;

            if (i < proposed.Count && !rule.IsValid(v))
                return false;

            normalized.Add(rule.Sanitize(v));
        }

        for (int i = fixedRules.Count; i < proposed.Count; i++)
        {
            var v = proposed[i];

            if (!tailRule.IsValid(v))
                return false;

            normalized.Add(tailRule.Sanitize(v));
        }

        return true;
    }

    protected virtual bool GetValidStuffIds(List<int> proposed, out List<int> stuffIds)
    {
        stuffIds = [];

        var count = 0;

        foreach (var id in proposed)
        {
            if (!_roomGrain._state.ItemsById.TryGetValue(id, out var item))
                continue;

            stuffIds.Add(id);

            count++;

            if (count >= _roomGrain._roomConfig.WiredSelectedItemsLimit)
                break;
        }

        return true;
    }

    protected virtual bool GetValidVariableIds(
        List<string> proposed,
        out List<WiredVariableId> variableIds
    )
    {
        variableIds = [];

        var count = 0;
        var max = GetMaxVariableIds();

        foreach (var id in proposed)
        {
            try
            {
                var variableId = WiredVariableId.Parse(id);
                var variable = _roomGrain.WiredSystem.GetVariableById(variableId);

                if (variable is null)
                    continue;

                variableIds.Add(variableId);

                count++;

                if (count >= max)
                    break;
            }
            catch (Exception ex)
            {
                LogWiredDataFault(ex);
                continue;
            }
        }

        return true;
    }

    protected virtual Task FillInternalDataAsync(CancellationToken ct)
    {
        _snapshot = null;

        if (_wiredData is null)
        {
            if (
                _ctx.RoomObject.ExtraData.TryGetSection(
                    ExtraDataSectionType.WIRED,
                    out var wiredDataElement
                )
            )
            {
                _wiredData = wiredDataElement.Deserialize<WiredData>() ?? new WiredData();
            }
            else
            {
                _wiredData = new WiredData();
            }

            _wiredData.AttatchRules(GetIntParamRules());
        }

        if (TryNormalizeIntParams(_wiredData.IntParams, out var normalizedIntParams))
        {
            if (!_wiredData.IntParams.SequenceEqual(normalizedIntParams))
            {
                _wiredData.IntParams = normalizedIntParams;
                _wiredData.MarkDirty();
            }
        }

        if (GetValidStuffIds(_wiredData.StuffIds, out var stuffIds))
        {
            if (!_wiredData.StuffIds.SequenceEqual(stuffIds))
            {
                _wiredData.StuffIds = stuffIds;

                _wiredData.MarkDirty();
            }
        }

        if (GetValidStuffIds(_wiredData.StuffIds2, out var stuffIds2))
        {
            if (!_wiredData.StuffIds2.SequenceEqual(stuffIds2))
            {
                _wiredData.StuffIds2 = stuffIds2;

                _wiredData.MarkDirty();
            }
        }

        if (GetValidVariableIds(_wiredData.VariableIds, out var variableIds))
        {
            var variableIdStrings = variableIds.Select(x => x.ToString()).ToList();

            if (!_wiredData.VariableIds.SequenceEqual(variableIdStrings))
            {
                _wiredData.VariableIds = variableIdStrings;

                _wiredData.MarkDirty();
            }
        }

        _wiredData.SetAction(() =>
        {
            _ctx.RoomObject.ExtraData.UpdateSection(
                ExtraDataSectionType.WIRED,
                JsonSerializer.SerializeToNode(_wiredData, _wiredData.GetType())
            );

            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    public WiredDataSnapshot GetSnapshot() => _snapshot ??= BuildSnapshot();

    protected virtual WiredDataSnapshot BuildSnapshot() =>
        new()
        {
            WiredType = WiredType,
            FurniLimit = _roomGrain._roomConfig.WiredSelectedItemsLimit,
            StuffIds = GetValidStuffIds(_wiredData.StuffIds, out var validStuffIds)
                ? validStuffIds
                : [],
            StuffIds2 = GetValidStuffIds(_wiredData.StuffIds2, out var validStuffIds2)
                ? validStuffIds2
                : [],
            StuffTypeId = _ctx.Definition.SpriteId,
            Id = _ctx.ObjectId,
            StringParam = _wiredData.StringParam,
            IntParams = _wiredData.IntParams,
            VariableIds = GetValidVariableIds(_wiredData.VariableIds, out var validVariableIds)
                ? validVariableIds
                : [],
            FurniSourceTypes = GetFurniSources(),
            PlayerSourceTypes = GetPlayerSources(),
            Code = WiredCode,
            AdvancedMode = SupportsAdvancedMode(),
            AmountFurniSelections = [],
            AllowWallFurni = _roomGrain._roomConfig.WiredAllowWallFurni,
            AllowedFurniSources = GetAllowedFurniSources(),
            AllowedPlayerSources = GetAllowedPlayerSources(),
            DefaultFurniSources = GetDefaultFurniSources(),
            DefaultPlayerSources = GetDefaultPlayerSources(),
            DefinitionSpecifics = GetDefinitionSpecifics(),
            TypeSpecifics = GetTypeSpecifics(),
            ContextSnapshots = GetWiredContextSnapshots(),
            DefaultIntParams = GetDefaultIntParams(),
        };

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        await OnWiredStackChangedAsync(
            ActionContext.CreateForSystem(_roomGrain.RoomId),
            [_ctx.GetTileIdx()],
            ct
        );
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);

        await OnWiredStackChangedAsync(
            ActionContext.CreateForSystem(_roomGrain.RoomId),
            [_ctx.GetTileIdx()],
            ct
        );
    }

    public override Task OnStateChangedAsync(CancellationToken ct) => Task.CompletedTask;

    public override async Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct)
    {
        await base.OnMoveAsync(ctx, prevIdx, ct);

        await OnWiredStackChangedAsync(ctx, [_ctx.GetTileIdx(), prevIdx], ct);
    }

    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        await base.OnPickupAsync(ctx, ct);

        _ctx.RoomObject.ExtraData.DeleteSection(ExtraDataSectionType.WIRED);

        await OnWiredStackChangedAsync(ctx, [_ctx.GetTileIdx()], ct);
    }

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        _ = _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync(new OpenEventMessageComposer { ItemId = _ctx.ObjectId }, ct)
            .ConfigureAwait(false);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stored wired data that does not fit the box (a stale shape after a definition change) is
    /// replaced by defaults; the fault is logged so the loss is visible.
    /// </summary>
    protected void LogWiredDataFault(Exception ex) =>
        _roomGrain._logger.LogDebug(
            ex,
            "Wired item {ItemId} in room {RoomId} carried data its box could not read",
            _ctx.ObjectId,
            _ctx.RoomId
        );

    protected bool TryGetFloorItem(int itemId, out IRoomFloorItem floorItem)
    {
        floorItem = null!;

        if (
            !_roomGrain._state.ItemsById.TryGetValue(itemId, out var item)
            || item is not IRoomFloorItem found
        )
            return false;

        floorItem = found;

        return true;
    }

    protected bool TryGetPlayer(int playerId, out IRoomPlayer player)
    {
        player = null!;

        if (
            !_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
            || avatar is not IRoomPlayer found
        )
            return false;

        player = found;

        return true;
    }

    /// <summary>The players of a selection that are still in the room.</summary>
    protected List<IRoomPlayer> GetPlayers(IWiredSelectionSet selection)
    {
        var players = new List<IRoomPlayer>();

        foreach (var playerId in selection.SelectedPlayerIds)
        {
            if (TryGetPlayer(playerId, out var player))
                players.Add(player);
        }

        return players;
    }

    /// <summary>The floor items of a selection that are still in the room.</summary>
    protected List<IRoomFloorItem> GetFloorItems(IWiredSelectionSet selection)
    {
        var items = new List<IRoomFloorItem>();

        foreach (var itemId in selection.SelectedFurniIds)
        {
            if (TryGetFloorItem(itemId, out var item))
                items.Add(item);
        }

        return items;
    }

    /// <summary>An int param, or the default when the box has fewer params than expected.</summary>
    protected T GetIntParamOrDefault<T>(int index, T fallback)
    {
        try
        {
            if (_wiredData is null || index >= _wiredData.IntParams.Count)
                return fallback;

            return _wiredData.GetIntParam<T>(index);
        }
        catch (Exception ex)
        {
            LogWiredDataFault(ex);

            return fallback;
        }
    }

    protected virtual Task OnWiredStackChangedAsync(
        ActionContext ctx,
        List<int> ids,
        CancellationToken ct
    ) =>
        _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                StackIds = ids,
            },
            ct
        );
}
