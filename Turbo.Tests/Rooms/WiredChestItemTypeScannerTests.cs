using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "WIRED Scanner Add-on: Chest Furni of Type" counts the items of the picked furni types in the
/// picked chests into a context variable before the effects run. Here a "Change Variable Value"
/// on the same stack copies that context variable into a global, so the global shows what the
/// scan stored. The chest holds three of type 500 and two of type 600 and previews 500, 600, 500.
/// </summary>
public sealed class WiredChestItemTypeScannerTests
{
    private const int CLICK_ME = 23;
    private const int TYPE_500 = 30;
    private const int CHEST = 40;
    private const int GLOBAL = 10;
    private const int CONTEXT = 11;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _global = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredChestItemTypeScannerTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(TYPE_500, 6, 6, definitionId: 500);
    }

    [Fact]
    public async Task Scanning_all_items_counts_every_item_of_the_type_in_the_chest()
    {
        await BuildAsync(previewedOnly: false);

        await ClickAsync();
        await TickAsync(4);

        Global().Should().Be(3);
    }

    [Fact]
    public async Task Scanning_previewed_items_counts_only_the_items_the_chest_shows()
    {
        await BuildAsync(previewedOnly: true);

        await ClickAsync();
        await TickAsync(4);

        Global().Should().Be(2);
    }

    private int Global()
    {
        var snapshot = _global.GetVarSnapshot();

        _global
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return value;
    }

    private async Task BuildAsync(bool previewedOnly)
    {
        var chest = (FurnitureWiredChestLogic)
            _room
                .AddFloorItem(
                    CHEST,
                    5,
                    5,
                    "wired_chest_furni",
                    createLogic: (factory, ctx) => new FurnitureWiredFurniChestLogic(factory, ctx)
                )
                .Logic;

        _global = _room.AddBox<WiredVariableRoom>(GLOBAL, 4, 4, "wf_var_room");
        var context = _room.AddBox<WiredVariableContext>(CONTEXT, 3, 3, "wf_var_context");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                GLOBAL,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "count"
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                CONTEXT,
                intParams: [1],
                stringParam: "scanned"
            )
        )
            .Should()
            .BeTrue();
        await _global.LoadWiredAsync(Ct);
        await context.LoadWiredAsync(Ct);
        await StartAsync(4, 4);
        await StartAsync(3, 3);

        var globalId = _global.GetVarSnapshot().VariableId.ToString();
        var contextId = context.GetVarSnapshot().VariableId.ToString();

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredAddonChestItemTypeScanner>(2, 0, 0, "wf_xtra_scan_chest_furni_by_type");
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                2,
                intParams: [previewedOnly ? 1 : 0],
                stuffIds: [TYPE_500],
                stuffIds2: [CHEST],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectedItems],
                ],
                variableIds: [contextId]
            )
        ).Should().BeTrue();
        // Set the global to the context variable's value.
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    1,
                    0,
                    0,
                    (int)WiredVariableTargetType.Context,
                ],
                definitionSpecifics: [0],
                variableIds: [globalId, contextId]
            )
        )
            .Should()
            .BeTrue();

        await StartAsync(0, 0);

        // What the chest grain reports it holds, as the chest keeps it for its conditions and scanners.
        ChestItemTypeSnapshot Type(int id) =>
            new()
            {
                IsWallItem = false,
                TypeId = id,
                LegacyPosterId = "",
            };
        // Summary's setter is private to the chest base class.
        typeof(FurnitureWiredChestLogic)
            .GetProperty(nameof(FurnitureWiredChestLogic.Summary))!
            .SetValue(
                chest,
                WiredChestSummarySnapshot.Empty with
                {
                    ItemCount = 5,
                    CountsByType = ImmutableDictionary<ChestItemTypeSnapshot, int>
                        .Empty.Add(Type(500), 3)
                        .Add(Type(600), 2),
                    Preview = [Type(500), Type(600), Type(500)],
                }
            );
    }

    private async Task StartAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private Task ClickAsync() =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
