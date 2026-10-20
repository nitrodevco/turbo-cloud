using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A save that picks furni a box cannot use is refused with the hotel's own text instead of
/// being saved and then doing nothing: <c>wiredfurni.error.require_antenna_furni</c>,
/// <c>require_coin_chests</c>, <c>require_furni_chests</c>, <c>require_chests</c>,
/// <c>require_contracts</c>, <c>require_contracts_or_trade_wired</c>, <c>require_room_linker</c>
/// and <c>temp_furni_not_allowed</c> (official external_flash_texts 2026-10-13). The texts are
/// Habbo's; which box sends which is inferred from their wording. A slot refuses only the pick
/// list it reads, so Send Signal still forwards any furni.
/// </summary>
public sealed class WiredPickRefusalTests
{
    private const int BOX = 1;
    private const int ANTENNA = 10;
    private const int LAMP = 11;
    private const int COIN_CHEST = 12;
    private const int FURNI_CHEST = 13;
    private const int CONTRACT = 14;
    private const int LINKER = 15;
    private const int INIT_BOX = 16;

    private readonly WiredRoom _room = new(8, 8);

    public WiredPickRefusalTests()
    {
        _room.AddFloorItem(ANTENNA, 2, 2, "wf_antenna1");
        _room.AddFloorItem(LAMP, 3, 2);
    }

    [Fact]
    public async Task Send_signal_refuses_a_furni_that_is_no_antenna_as_its_antenna()
    {
        _room.AddBox<WiredActionSendSignal>(BOX, 0, 0, "wf_act_send_signal");

        var refused = await SaveWithResultAsync<UpdateActionMessage>(stuffIds: [LAMP]);

        refused.IsSaved.Should().BeFalse();
        refused.ErrorKey.Should().Be("wiredfurni.error.require_antenna_furni");
        (await SaveAsync<UpdateActionMessage>(stuffIds: [ANTENNA])).Should().BeTrue();
    }

    [Fact]
    public async Task Send_signal_forwards_any_furni_its_antenna_slot_does_not_read()
    {
        _room.AddBox<WiredActionSendSignal>(BOX, 0, 0, "wf_act_send_signal");

        // Antennas from the secondary picks, the lamp forwarded from the picked furni.
        (
            await SaveAsync<UpdateActionMessage>(
                stuffIds: [LAMP],
                stuffIds2: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SecondaryItems],
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        )
            .Should()
            .BeTrue();

        // The same lamp as the antennas' list is refused.
        var refused = await SaveWithResultAsync<UpdateActionMessage>(
            stuffIds: [ANTENNA],
            stuffIds2: [LAMP],
            furniSources:
            [
                [WiredFurniSourceType.SecondaryItems],
                [WiredFurniSourceType.SelectedItems],
            ]
        );

        refused.ErrorKey.Should().Be("wiredfurni.error.require_antenna_furni");
    }

    [Fact]
    public async Task Receive_signal_listens_on_antennas_only()
    {
        _room.AddBox<WiredTriggerReceiveSignal>(BOX, 0, 0, "wf_trg_recv_signal");

        (await SaveWithResultAsync<UpdateTriggerMessage>(stuffIds: [ANTENNA, LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_antenna_furni");
        (await SaveAsync<UpdateTriggerMessage>(stuffIds: [ANTENNA])).Should().BeTrue();
    }

    [Fact]
    public async Task Which_furni_are_antennas_is_configured()
    {
        Configure(("AntennaLogicNames:0", "default_floor"));
        _room.AddBox<WiredActionSendSignal>(BOX, 0, 0, "wf_act_send_signal");

        (await SaveAsync<UpdateActionMessage>(stuffIds: [LAMP])).Should().BeTrue();
    }

    [Fact]
    public async Task Give_credits_takes_wired_credit_chests_only()
    {
        await AddChestsAsync(wiredEnabled: true);
        _room.AddBox<WiredActionGiveCurrencyFromChest>(BOX, 0, 0, "wf_act_give_currency");

        (await SaveWithResultAsync<UpdateActionMessage>(stuffIds: [FURNI_CHEST]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_coin_chests");
        (await SaveWithResultAsync<UpdateActionMessage>(stuffIds: [LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_coin_chests");
        (await SaveAsync<UpdateActionMessage>(stuffIds: [COIN_CHEST])).Should().BeTrue();
    }

    [Fact]
    public async Task An_effect_refuses_a_normal_chest_that_is_not_a_wired_chest()
    {
        await AddChestsAsync(wiredEnabled: false);
        _room.AddBox<WiredActionGiveFurniFromChest>(BOX, 0, 0, "wf_act_give_furni");

        (await SaveWithResultAsync<UpdateActionMessage>(stuffIds: [FURNI_CHEST]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_furni_chests");
    }

    [Fact]
    public async Task Chest_has_items_takes_chests_of_either_kind_wired_or_not()
    {
        await AddChestsAsync(wiredEnabled: false);
        _room.AddBox<WiredConditionChestHasItems>(BOX, 0, 0, "wf_cnd_chest_has_items");

        (await SaveWithResultAsync<UpdateConditionMessage>(stuffIds: [LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_chests");
        (await SaveAsync<UpdateConditionMessage>(stuffIds: [COIN_CHEST, FURNI_CHEST]))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task Initiate_transaction_takes_contracts_in_its_contract_slot()
    {
        AddContract();
        _room.AddBox<WiredActionInitiateTransaction>(BOX, 0, 0, "wf_act_init_transaction");

        (await SaveWithResultAsync<UpdateActionMessage>(stuffIds2: [LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_contracts");
        (await SaveAsync<UpdateActionMessage>(stuffIds2: [CONTRACT])).Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_transaction_takes_contracts_or_an_initiate_transaction_box()
    {
        AddContract();
        _room.AddBox<WiredActionInitiateTransaction>(INIT_BOX, 6, 6, "wf_act_init_transaction");
        _room.AddBox<WiredActionCancelTransaction>(BOX, 0, 0, "wf_act_cancel_transaction");

        (await SaveWithResultAsync<UpdateActionMessage>(stuffIds: [LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_contracts_or_trade_wired");
        (await SaveAsync<UpdateActionMessage>(stuffIds: [CONTRACT, INIT_BOX])).Should().BeTrue();
    }

    [Fact]
    public async Task Transaction_triggers_take_contracts_or_an_initiate_transaction_box()
    {
        AddContract();
        _room.AddBox<WiredTriggerTransactionCompleted>(BOX, 0, 0, "wf_trg_transaction_complete");

        (await SaveWithResultAsync<UpdateTriggerMessage>(stuffIds: [LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_contracts_or_trade_wired");
        (await SaveAsync<UpdateTriggerMessage>(stuffIds: [CONTRACT])).Should().BeTrue();
    }

    [Fact]
    public async Task Teleport_to_room_takes_room_linkers()
    {
        _room.AddFloorItem(
            LINKER,
            5,
            5,
            TeleportFurniture.ROOM_LINKER_CLASSNAME,
            createLogic: (stuffData, ctx) => new FurnitureRoomLinkerLogic(stuffData, ctx)
        );
        _room.AddBox<WiredActionTeleportToRoom>(BOX, 0, 0, "wf_act_teleport_to_room");

        (await SaveWithResultAsync<UpdateActionMessage>(stuffIds: [LAMP]))
            .ErrorKey.Should()
            .Be("wiredfurni.error.require_room_linker");
        (await SaveAsync<UpdateActionMessage>(stuffIds: [LINKER])).Should().BeTrue();
    }

    [Fact]
    public async Task A_temporary_furni_cannot_be_picked()
    {
        _room.AddFloorItem(FurniIdBands.TemporaryMin, 4, 4);
        _room.AddBox<WiredActionToggleItemState>(BOX, 0, 0, "wf_act_toggle_state");

        var refused = await SaveWithResultAsync<UpdateActionMessage>(
            stuffIds: [LAMP, FurniIdBands.TemporaryMin]
        );

        refused.IsSaved.Should().BeFalse();
        refused.ErrorKey.Should().Be("wiredfurni.error.temp_furni_not_allowed");
    }

    [Fact]
    public async Task Temporary_furni_can_be_picked_when_configured()
    {
        Configure(("RefuseTemporaryFurniPicks", "false"));
        _room.AddFloorItem(FurniIdBands.TemporaryMin, 4, 4);
        _room.AddBox<WiredActionToggleItemState>(BOX, 0, 0, "wf_act_toggle_state");

        (await SaveAsync<UpdateActionMessage>(stuffIds: [LAMP, FurniIdBands.TemporaryMin]))
            .Should()
            .BeTrue();
    }

    // Each box's own int params, so the picks are all that is checked.
    private Task<WiredSaveResult> SaveWithResultAsync<TMessage>(
        int[]? stuffIds = null,
        int[]? stuffIds2 = null,
        WiredFurniSourceType[][]? furniSources = null
    )
        where TMessage : UpdateWiredMessage =>
        _room.SaveWithResultAsync<TMessage>(
            BOX,
            intParams:
            [
                .. ((FurnitureWiredLogic)_room.FloorItem(BOX).Logic)
                    .GetIntParamRules()
                    .Select(x => x.DefaultValue),
            ],
            stuffIds: stuffIds,
            stuffIds2: stuffIds2,
            furniSources: furniSources
        );

    private async Task<bool> SaveAsync<TMessage>(
        int[]? stuffIds = null,
        int[]? stuffIds2 = null,
        WiredFurniSourceType[][]? furniSources = null
    )
        where TMessage : UpdateWiredMessage =>
        (await SaveWithResultAsync<TMessage>(stuffIds, stuffIds2, furniSources)).IsSaved;

    // As the server's settings bind it.
    private void Configure(params (string Key, string Value)[] settings) =>
        RoomHarness.SetField(
            _room.Harness.Room,
            "_wiredConfig",
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings.ToDictionary(x => x.Key, x => (string?)x.Value))
                .Build()
                .Get<WiredConfig>()!
        );

    private async Task AddChestsAsync(bool wiredEnabled)
    {
        foreach (
            var (id, logic) in new[]
            {
                (COIN_CHEST, "wired_chest_coins"),
                (FURNI_CHEST, "wired_chest_furni"),
            }
        )
        {
            var chest = (FurnitureWiredChestLogic)
                _room
                    .AddFloorItem(
                        id,
                        5,
                        id - 8,
                        logic,
                        createLogic: (factory, ctx) =>
                            id == COIN_CHEST
                                ? new FurnitureWiredCoinsChestLogic(factory, ctx)
                                : new FurnitureWiredFurniChestLogic(factory, ctx)
                    )
                    .Logic;

            await chest.SetMapDataAsync(
                new Dictionary<string, string>
                {
                    [WiredChestData.IS_WIRED_ENABLED] = wiredEnabled
                        ? WiredChestData.TRUE
                        : WiredChestData.FALSE,
                },
                refresh: false
            );
        }
    }

    private void AddContract() =>
        _room.AddFloorItem(
            CONTRACT,
            4,
            6,
            "wired_contract_payment",
            createLogic: (factory, ctx) => new FurnitureWiredPaymentContractLogic(factory, ctx)
        );
}
