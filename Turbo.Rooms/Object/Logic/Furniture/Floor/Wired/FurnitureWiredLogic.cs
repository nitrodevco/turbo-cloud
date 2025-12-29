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
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;

public abstract class FurnitureWiredLogic : FurnitureFloorLogic, IFurnitureWiredLogic
{
    protected readonly IWiredDataFactory _wiredDataFactory;
    protected readonly IGrainFactory _grainFactory;

    public abstract WiredType WiredType { get; }
    public abstract int WiredCode { get; }
    public IWiredData WiredData { get; private set; }

    protected override StuffPersistanceType _stuffPersistanceType => StuffPersistanceType.Internal;

    protected int _furniLimit = 20;
    protected int _flashDelayMs = 1500;
    protected int _lastFlashMs = 0;
    protected bool _advancedMode = true;
    protected bool _allowWallFurni = false;

    private WiredDataSnapshot? _snapshot;

    public int Id => _ctx.ObjectId.Value;

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

        WiredData = _wiredDataFactory.CreateWiredDataFromExtraData(WiredType, ctx.Item.ExtraData);

        WiredData.SetAction(async () =>
        {
            _ctx.Item.ExtraData.UpdateSection(
                "wired",
                JsonSerializer.SerializeToNode(WiredData, WiredData.GetType())
            );
        });
    }

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        _ = _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = null,
                StackIds = [_ctx.GetTileIdx()],
            },
            ct
        );
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);

        _ = _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = null,
                StackIds = [_ctx.GetTileIdx()],
            },
            ct
        );
    }

    public override async Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct)
    {
        await base.OnMoveAsync(ctx, prevIdx, ct);

        _ = _ctx.PublishRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                StackIds = [_ctx.GetTileIdx(), prevIdx],
            },
            ct
        );
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        _ = _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync(new OpenEventMessageComposer { ItemId = _ctx.ObjectId })
            .ConfigureAwait(false);
    }

    public async Task LoadWiredAsync(CancellationToken ct)
    {
        await FillInternalDataAsync(ct);
    }

    public async Task FlashActivationStateAsync()
    {
        var state = await GetStateAsync() == 1 ? 0 : 1;

        _ = SetStateAsync(state);
    }

    public virtual List<WiredFurniSourceType[]> GetFurniSources()
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

    public virtual List<WiredPlayerSourceType[]> GetPlayerSources()
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

    public virtual List<WiredFurniSourceType[]> GetAllowedFurniSources() => [];

    public virtual List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [];

    public virtual List<WiredFurniSourceType[]> GetDefaultFurniSources() =>
        [.. GetAllowedFurniSources().Select(x => new[] { x[0] })];

    public virtual List<WiredPlayerSourceType[]> GetDefaultPlayerSources() =>
        [.. GetAllowedPlayerSources().Select(x => new[] { x[0] })];

    public virtual List<object> GetDefinitionSpecifics() => [];

    public virtual List<object> GetTypeSpecifics() => [];

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
            var variableIds = new List<long>();
            var furniSources = new List<WiredFurniSourceType[]>();
            var playerSources = new List<WiredPlayerSourceType[]>();
            var definitionSpecifics = new List<object>();
            var typeSpecifics = new List<object>();

            if (update.IntParams.Count > 0)
            {
                foreach (var intParam in update.IntParams)
                    intParams.Add(intParam);
            }

            if (update.StuffIds.Count > 0)
            {
                var count = 0;

                foreach (var id in update.StuffIds)
                {
                    var snapshot = await _ctx.GetFloorItemSnapshotByIdAsync(id, ct);

                    if (snapshot is null)
                        continue;

                    stuffIds.Add(id);

                    count++;

                    if (count >= _furniLimit)
                        break;
                }
            }

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

            foreach (var defSpecific in GetDefinitionSpecifics())
            {
                object specific = defSpecific;

                try
                {
                    if (update.DefinitionSpecifics[index] is not null)
                    {
                        specific = update.DefinitionSpecifics[index];
                    }
                }
                catch { }

                definitionSpecifics.Add(specific);
                index++;
            }

            index = 0;

            foreach (var typeSpecific in GetTypeSpecifics())
            {
                object specific = typeSpecific;

                try
                {
                    if (update.TypeSpecifics[index] is not null)
                    {
                        specific = update.TypeSpecifics[index];
                    }
                }
                catch { }

                typeSpecifics.Add(specific);
                index++;
            }

            WiredData.IntParams = intParams;
            WiredData.StringParam = stringParam;
            WiredData.StuffIds = stuffIds;
            WiredData.VariableIds = variableIds;
            WiredData.FurniSources = furniSources;
            WiredData.PlayerSources = playerSources;
            WiredData.DefinitionSpecifics = definitionSpecifics;
            WiredData.TypeSpecifics = typeSpecifics;

            WiredData.MarkDirty();

            _ = _ctx.PublishRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = _ctx.RoomId,
                    CausedBy = ctx,
                    StackIds = [_ctx.GetTileIdx()],
                },
                ct
            );

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }

    protected virtual Task FillInternalDataAsync(CancellationToken ct) => Task.CompletedTask;

    public WiredDataSnapshot GetSnapshot()
    {
        _snapshot = BuildSnapshot();

        return _snapshot;
    }

    protected virtual WiredDataSnapshot BuildSnapshot() =>
        new()
        {
            WiredType = WiredType,
            FurniLimit = _furniLimit,
            StuffIds = WiredData.StuffIds,
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
        };
}
