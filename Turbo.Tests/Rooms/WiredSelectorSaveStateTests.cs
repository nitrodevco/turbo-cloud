using FluentAssertions;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Userdefinedroomevents;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo answers a selector's save with ObjectDataUpdate(selector, "0") and then
/// WiredSaveSuccess, any other box's save with WiredSaveSuccess alone (official client captures
/// 2026-10-14 09:46 and 2026-10-16 06:10/06:11/07:03, 6 of 6 selector saves). Turbo sent no
/// ObjectDataUpdate, and sent WiredSaveSuccess straight to the session, ahead of anything the
/// room sent the player through their presence.
/// </summary>
public sealed class WiredSelectorSaveStateTests
{
    private const int OWNER = 1;
    private const int SELECTOR = 4;
    private const int EFFECT = 3;
    private const int PAD = 21;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_saved_selector_rests_again_and_the_saver_is_told_first()
    {
        var selector = AddSelector();
        await selector.SetStateAsync(2, false);

        (await SaveSelectorAsync(filter: false)).IsSaved.Should().BeTrue();

        selector.GetState().Should().Be(0);
        StatesSentTo(OWNER).Should().Equal("0");

        var published = Published().Single();
        published.ExcludedPlayerIds.Should().Equal((PlayerId)OWNER);
        published
            .Composers.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<ObjectDataUpdateMessageComposer>();
    }

    /// <summary>Inference: a filter rests at 3 (its flash is 4/5), so its save puts it back there.</summary>
    [Fact]
    public async Task A_saved_filter_rests_at_3()
    {
        var selector = AddSelector();

        (await SaveSelectorAsync(filter: true)).IsSaved.Should().BeTrue();

        selector.GetState().Should().Be(3);
        StatesSentTo(OWNER).Should().Equal("3");
    }

    [Fact]
    public async Task An_effect_save_sends_no_state()
    {
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        _room.AddFloorItem(PAD, 3, 3);
        _room.Harness.Fakes.Log.Calls.Clear();

        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [PAD],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        Published().Should().BeEmpty();
        StatesSentTo(OWNER).Should().BeEmpty();
    }

    [Fact]
    public async Task The_save_is_answered_through_the_presence_after_what_the_room_sent_there()
    {
        var fakes = new Fakes();
        fakes.Handlers[nameof(IRoomGrain.ApplyWiredUpdateAsync)] = _ =>
            Task.FromResult(WiredSaveResult.Saved);

        await new UpdateSelectorMessageHandler(fakes.Create<IGrainFactory>()).HandleAsync(
            Message(filter: false),
            new MessageContext(fakes.Create<ISessionContext>(), OWNER, 1),
            Ct
        );

        fakes
            .Log.Of("SendComposerAsync")
            .Where(x => x.Interface == typeof(IPlayerPresenceGrain) && Equals(x.Key, (long)OWNER))
            .Select(x => x.Args[0])
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<WiredSaveSuccessEventMessageComposer>();
        fakes
            .Log.Of("SendComposerAsync")
            .Where(x => x.Interface == typeof(ISessionContext))
            .Should()
            .BeEmpty();
    }

    private WiredSelectorItemsByType AddSelector()
    {
        _room.AddFloorItem(PAD, 3, 3);
        var selector = _room.AddBox<WiredSelectorItemsByType>(
            SELECTOR,
            0,
            0,
            "wf_slc_furni_bytype"
        );
        _room.Harness.Fakes.Log.Calls.Clear();

        return selector;
    }

    private Task<WiredSaveResult> SaveSelectorAsync(bool filter) =>
        _room.Harness.Room.ApplyWiredUpdateAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            SELECTOR,
            Message(filter),
            Ct
        );

    private static UpdateSelectorMessage Message(bool filter) =>
        new UpdateSelectorMessage
        {
            Id = SELECTOR,
            IntParams = [0],
            StringParam = "",
            StuffIds = [PAD],
            StuffIds2 = [],
            DefinitionSpecifics = [filter, false],
            FurniSources =
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            PlayerSources = [],
            VariableIds = [],
            TypeSpecifics = [],
        };

    private List<string> StatesSentTo(int player) =>
        [
            .. _room
                .Harness.Fakes.Log.Of("SendComposerAsync")
                .Where(x =>
                    x.Interface == typeof(IPlayerPresenceGrain) && Equals(x.Key, (long)player)
                )
                .Select(x => x.Args[0])
                .OfType<ObjectDataUpdateMessageComposer>()
                .Select(x => ((LegacyStuffSnapshot)x.StuffData).Data),
        ];

    private List<RoomOutboundSnapshot> Published() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("OnNextAsync")
                .Select(call => (RoomOutboundSnapshot)call.Args[0]!)
                .Where(x => x.Composers.Any(c => c is ObjectDataUpdateMessageComposer)),
        ];
}
