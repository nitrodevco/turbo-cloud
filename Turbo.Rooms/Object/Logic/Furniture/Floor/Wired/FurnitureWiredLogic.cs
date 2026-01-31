using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

public abstract class FurnitureWiredLogic : FurnitureFloorLogic, IWiredBox
{
    protected readonly IWiredDataFactory _wiredDataFactory;
    protected readonly IGrainFactory _grainFactory;

    public abstract WiredType WiredType { get; }
    public abstract int WiredCode { get; }
    public IWiredData WiredData { get; private set; }

    protected override StuffPersistanceType _stuffPersistanceType =>
        StuffPersistanceType.RoomActive;

    protected int _furniLimit = 20;
    protected bool _advancedMode = true;
    protected bool _allowWallFurni = false;

    protected virtual int MinIntParams => GetIntParamRules().Count;
    protected virtual int MaxIntParams => 16;

    private WiredDataSnapshot? _snapshot;

    public FurnitureWiredLogic(
        IWiredDataFactory wiredDataFactory,
        IGrainFactory grainFactory,
        IStuffDataFactory stuffDataFactory,
        IRoomFloorItemContext ctx
    )
        : base(stuffDataFactory, ctx)
    {
        _wiredDataFactory = wiredDataFactory;
        _grainFactory = grainFactory;

        WiredData = _wiredDataFactory.CreateWiredDataFromExtraData(
            WiredType,
            _ctx.RoomObject.ExtraData
        );

        WiredData.SetAction(() =>
        {
            _ctx.RoomObject.ExtraData.UpdateSection(
                ExtraDataSectionType.WIRED,
                JsonSerializer.SerializeToNode(WiredData, WiredData.GetType())
            );
            return Task.CompletedTask;
        });
    }

    public async Task LoadWiredAsync(CancellationToken ct)
    {
        FillIntParamsIfEmpty();

        await FillInternalDataAsync(ct);
    }

    public Task FlashActivationStateAsync(CancellationToken ct) =>
        SetStateAsync(GetState() == 1 ? 0 : 1);

    public virtual List<int> GetStuffIds()
    {
        if (GetValidStuffIds(WiredData.StuffIds, out var stuffIds))
        {
            if (!WiredData.StuffIds.SequenceEqual(stuffIds))
            {
                WiredData.StuffIds = stuffIds;

                WiredData.MarkDirty();
            }
        }

        return stuffIds ?? [];
    }

    public virtual List<int> GetStuffIds2()
    {
        if (GetValidStuffIds(WiredData.StuffIds2, out var stuffIds))
        {
            if (!WiredData.StuffIds2.SequenceEqual(stuffIds))
            {
                WiredData.StuffIds2 = stuffIds;

                WiredData.MarkDirty();
            }
        }

        return stuffIds ?? [];
    }

    public virtual List<IWiredIntParamRule> GetIntParamRules() => [];

    public virtual IWiredIntParamRule? GetIntParamTailRule() => null;

    public virtual List<WiredFurniSourceType[]> GetAllowedFurniSources() => [];

    public virtual List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [];

    public virtual List<Type> GetDefinitionSpecificTypes() => [];

    public virtual List<Type> GetTypeSpecificTypes() => [];

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
                if (WiredData.FurniSources[index] is not null)
                {
                    sourceTypes = WiredData.FurniSources[index];
                }
            }
            catch { }

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
                if (WiredData.PlayerSources[index] is not null)
                {
                    sourceTypes = WiredData.PlayerSources[index];
                }
            }
            catch { }

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
                    WiredData.DefinitionSpecifics[index] is not null
                    && specType.IsAssignableFrom(WiredData.DefinitionSpecifics[index].GetType())
                )
                {
                    specific = WiredData.DefinitionSpecifics[index];
                }
            }
            catch { }

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
                    WiredData.TypeSpecifics[index] is not null
                    && specType.IsAssignableFrom(WiredData.TypeSpecifics[index].GetType())
                )
                {
                    specific = WiredData.TypeSpecifics[index];
                }
            }
            catch { }

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

            if (update.VariableIds.Count > 0)
            {
                foreach (var id in update.VariableIds)
                    variableIds.Add(id);
            }

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
                catch { }

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
                catch { }

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
                catch { }

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
                catch { }

                typeSpecifics.Add(specific);
                index++;
            }

            WiredData.IntParams = intParams;
            WiredData.StringParam = stringParam;
            WiredData.StuffIds = stuffIds;
            WiredData.StuffIds2 = stuffIds2;
            WiredData.VariableIds = variableIds;
            WiredData.FurniSources = furniSources;
            WiredData.PlayerSources = playerSources;
            WiredData.DefinitionSpecifics = definitionSpecifics;
            WiredData.TypeSpecifics = typeSpecifics;

            WiredData.MarkDirty();

            await OnWiredStackChangedAsync(ctx, [_ctx.GetTileIdx()], ct);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }

    protected virtual bool TryNormalizeIntParams(List<int> proposed, out List<int> normalized)
    {
        normalized = [];

        var fixedRules = GetIntParamRules();
        var tailRule = GetIntParamTailRule();
        var min = Math.Min(MinIntParams, MaxIntParams);
        var max = Math.Min(MaxIntParams, MaxIntParams);

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
                catch
                {
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

            if (count >= _furniLimit)
                break;
        }

        return true;
    }

    protected virtual Task FillInternalDataAsync(CancellationToken ct)
    {
        _snapshot = null;

        if (GetValidStuffIds(WiredData.StuffIds, out var stuffIds))
        {
            if (!WiredData.StuffIds.SequenceEqual(stuffIds))
            {
                WiredData.StuffIds = stuffIds;

                WiredData.MarkDirty();
            }
        }

        if (GetValidStuffIds(WiredData.StuffIds2, out var stuffIds2))
        {
            if (!WiredData.StuffIds2.SequenceEqual(stuffIds2))
            {
                WiredData.StuffIds2 = stuffIds2;

                WiredData.MarkDirty();
            }
        }

        return Task.CompletedTask;
    }

    protected virtual void FillIntParamsIfEmpty()
    {
        if (WiredData.IntParams is { Count: > 0 })
            return;

        var defaultInts = GetDefaultIntParams();

        WiredData.IntParams = defaultInts;
        WiredData.MarkDirty();
    }

    public WiredDataSnapshot GetSnapshot() => _snapshot ??= BuildSnapshot();

    protected virtual WiredDataSnapshot BuildSnapshot() =>
        new()
        {
            WiredType = WiredType,
            FurniLimit = _furniLimit,
            StuffIds = GetValidStuffIds(WiredData.StuffIds, out var validStuffIds)
                ? validStuffIds
                : [],
            StuffIds2 = GetValidStuffIds(WiredData.StuffIds2, out var validStuffIds2)
                ? validStuffIds2
                : [],
            StuffTypeId = _ctx.Definition.SpriteId,
            Id = _ctx.ObjectId,
            StringParam = WiredData.StringParam,
            IntParams = WiredData.IntParams,
            VariableIds = WiredData.VariableIds,
            FurniSourceTypes = GetFurniSources(),
            PlayerSourceTypes = GetPlayerSources(),
            Code = WiredCode,
            AdvancedMode = _advancedMode,
            AmountFurniSelections = [],
            AllowWallFurni = _allowWallFurni,
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
            .SendComposerAsync(new OpenEventMessageComposer { ItemId = _ctx.ObjectId })
            .ConfigureAwait(false);

        return Task.CompletedTask;
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
