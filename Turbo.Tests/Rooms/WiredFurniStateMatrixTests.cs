using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired matrix family W2 (furni state): States Match and Stuff Type Matches with their "not"
/// twins open with the furni quantifier radio (official client capture: codes 0, 8, 13 and 19 with
/// quantifierType 1, codes 1 and 14 with 0) and honour it, and Toggle To Random State may draw the
/// state a furni is already in, which changes nothing.
/// </summary>
public sealed class WiredFurniStateMatrixTests
{
    private const int BOX = 1;
    private const int LAMP_A = 30;
    private const int LAMP_B = 31;
    private const int QUANTIFIER_ALL = 0;
    private const int QUANTIFIER_ANY = 1;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // condition.0.E / condition.8.E (quantifier): the radio's wording comes from this type.
    [Fact]
    public void The_furni_conditions_offer_the_furni_quantifier()
    {
        var x = 0;
        IWiredCondition Add<T>(string logic)
            where T : FurnitureWiredConditionLogic => _room.AddBox<T>(BOX + ++x, x, 0, logic);

        Add<WiredConditionItemMatches>("wf_cnd_match_snapshot")
            .GetQuantifierType()
            .Should()
            .Be((byte)WiredQuantifierType.Furni);
        Add<WiredNegativeConditionItemMatches>("wf_cnd_not_match_snap")
            .GetQuantifierType()
            .Should()
            .Be((byte)WiredQuantifierType.Furni);
        Add<WiredConditionItemPerType>("wf_cnd_stuff_is")
            .GetQuantifierType()
            .Should()
            .Be((byte)WiredQuantifierType.Furni);
        Add<WiredNegativeConditionItemPerType>("wf_cnd_not_stuff_is")
            .GetQuantifierType()
            .Should()
            .Be((byte)WiredQuantifierType.Furni);

        // Furnis Have Avatars keeps its own "all / any" int param and shows no radio.
        Add<WiredConditionItemHasHabbo>("wf_cnd_furnis_hv_avtrs")
            .GetQuantifierType()
            .Should()
            .Be((byte)WiredQuantifierType.None);
        Add<WiredNegativeConditionItemHasHabbo>("wf_cnd_not_hv_avtrs")
            .GetQuantifierType()
            .Should()
            .Be((byte)WiredQuantifierType.None);
    }

    // condition.0 quantifier: lamp A stands as saved, lamp B was saved one tile further.
    // "All of the furni match" fails, "any of the furni match" holds; the not twin reads
    // "any of the furni does not match" (holds) and "none of the furni match" (fails).
    [Theory]
    [InlineData(false, QUANTIFIER_ALL, false)]
    [InlineData(false, QUANTIFIER_ANY, true)]
    [InlineData(true, QUANTIFIER_ALL, true)]
    [InlineData(true, QUANTIFIER_ANY, false)]
    public async Task States_match_counts_its_furni_with_the_quantifier(
        bool negative,
        int quantifier,
        bool passes
    )
    {
        _room.AddFloorItem(LAMP_A, 3, 3);
        _room.AddFloorItem(LAMP_B, 4, 4);

        var condition = negative
            ? _room.AddBox<WiredNegativeConditionItemMatches>(BOX, 0, 0, "wf_cnd_not_match_snap")
            : _room.AddBox<WiredConditionItemMatches>(BOX, 0, 0, "wf_cnd_match_snapshot");

        // Position only.
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [0, 0, 1, 0],
                stuffIds: [LAMP_A, LAMP_B],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [quantifier]
            )
        )
            .Should()
            .BeTrue();

        _room
            .FloorItem(BOX)
            .ExtraData.UpdateSection(
                WiredFurniSnapshotEntry.SECTION,
                new Dictionary<int, WiredFurniSnapshotEntry>
                {
                    [LAMP_A] = new(0, 0, 3, 3, 0),
                    [LAMP_B] = new(0, 0, 5, 4, 0),
                }
            );

        condition.Evaluate(Context()).Should().Be(passes);
    }

    // condition.8 quantifier: the furni to match (slot 0) are lamp A and a chair, the furni to
    // compare with (slot 1) lamp A. Habbo's default (code 0) is "all of the furni match"; ours
    // always took "any". (Slot 0's picks read both pick lists, so lamp A is picked there too.)
    [Theory]
    [InlineData(false, QUANTIFIER_ALL, false)]
    [InlineData(false, QUANTIFIER_ANY, true)]
    [InlineData(true, QUANTIFIER_ALL, true)]
    [InlineData(true, QUANTIFIER_ANY, false)]
    public async Task Stuff_type_matches_counts_its_furni_with_the_quantifier(
        bool negative,
        int quantifier,
        bool passes
    )
    {
        const int CHAIR = 32;

        _room.AddFloorItem(LAMP_A, 3, 3);
        _room.AddFloorItem(CHAIR, 5, 5);

        var condition = negative
            ? _room.AddBox<WiredNegativeConditionItemPerType>(BOX, 0, 0, "wf_cnd_not_stuff_is")
            : _room.AddBox<WiredConditionItemPerType>(BOX, 0, 0, "wf_cnd_stuff_is");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                stuffIds: [LAMP_A, CHAIR],
                stuffIds2: [LAMP_A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                ],
                definitionSpecifics: [quantifier]
            )
        ).Should().BeTrue();

        condition.Evaluate(Context()).Should().Be(passes);
    }

    // condition.8.R5: "Furni Matches" compares the furni themselves. The triggering lamp matches
    // a pick list holding it, and not one holding another lamp of the same kind (official client
    // capture 2026-10-24: same furni 4/4 true, another furni of the same kind, colour, state and
    // direction 8/8 false). Ours compared the kind.
    [Theory]
    [InlineData(LAMP_A, true)]
    [InlineData(LAMP_B, false)]
    public async Task Furni_matches_compares_the_furni_not_its_kind(int picked, bool passes)
    {
        const int LAMP_KIND = 500;

        _room.AddFloorItem(LAMP_A, 3, 3, definitionId: LAMP_KIND);
        _room.AddFloorItem(LAMP_B, 4, 4, definitionId: LAMP_KIND);

        var condition = _room.AddBox<WiredConditionItemPerType>(BOX, 0, 0, "wf_cnd_stuff_is");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                stuffIds: [picked],
                furniSources:
                [
                    [WiredFurniSourceType.TriggeredItem],
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [QUANTIFIER_ALL]
            )
        ).Should().BeTrue();

        var ctx = Context();
        ctx.Selected.SelectedFurniIds.Add(LAMP_A);

        condition.Evaluate(ctx).Should().Be(passes);
    }

    // condition.2.R1 / R5: Triggerer Is On Furni quantifies the users (quantifierType 2). Two
    // triggering users, one on the picked block: "all of the users match" fails, "any" holds.
    [Theory]
    [InlineData(QUANTIFIER_ALL, false)]
    [InlineData(QUANTIFIER_ANY, true)]
    public async Task Triggerer_on_furni_counts_its_users_with_the_quantifier(
        int quantifier,
        bool passes
    )
    {
        _room.AddFloorItem(LAMP_A, 3, 3);
        var on = _room.Enter(5, 3, 3);
        var off = _room.Enter(6, 6, 6);

        var condition = _room.AddBox<WiredConditionExecutorOnItem>(
            BOX,
            0,
            0,
            "wf_cnd_trggrer_on_frn"
        );

        condition.GetQuantifierType().Should().Be((byte)WiredQuantifierType.Users);

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                stuffIds: [LAMP_A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [quantifier]
            )
        ).Should().BeTrue();

        var ctx = Context();
        ctx.Selected.SelectedAvatarIds.Add(on.ObjectId);
        ctx.Selected.SelectedAvatarIds.Add(off.ObjectId);

        condition.Evaluate(ctx).Should().Be(passes);
    }

    // condition.1.R5: Furnis Have Avatars counts a user anywhere on a picked furni's tile, with
    // furni stacked between them (official client capture 2026-10-24: user on A, A on B, B
    // picked: true). condition.14.R3: the not twin is its exact inverse, over an empty selection
    // too: int 0 ("all have no avatars") true, int 1 ("any has no avatar") false.
    [Theory]
    [InlineData(false, 1, true, true)]
    [InlineData(false, 0, true, true)]
    [InlineData(true, 0, true, false)]
    [InlineData(true, 1, true, false)]
    [InlineData(true, 0, false, true)]
    [InlineData(true, 1, false, false)]
    public async Task Furnis_have_avatars_counts_users_above_the_picked_furni(
        bool negative,
        int requireAll,
        bool picked,
        bool passes
    )
    {
        _room.AddFloorItem(LAMP_B, 3, 3);
        _room.AddFloorItem(LAMP_A, 3, 3);
        _room.Enter(5, 3, 3);

        var condition = negative
            ? _room.AddBox<WiredNegativeConditionItemHasHabbo>(BOX, 0, 0, "wf_cnd_not_hv_avtrs")
            : _room.AddBox<WiredConditionItemHasHabbo>(BOX, 0, 0, "wf_cnd_furnis_hv_avtrs");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [requireAll],
                stuffIds: picked ? [LAMP_B] : [],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        condition.Evaluate(Context()).Should().Be(passes);
    }

    // action.0.B1: Toggle Furni State on a furni without states (a sofa) and a 2-state lamp:
    // the sofa is skipped, no state update for it (official client capture 2026-10-24).
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Toggling_skips_a_furni_without_states(int sofaStates)
    {
        const int SOFA = 32;

        var lamp = _room.AddFloorItem(LAMP_A, 3, 3);
        _room.AddFloorItem(SOFA, 5, 5, totalStates: sofaStates);
        var box = _room.AddBox<WiredActionToggleItemState>(BOX, 0, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0],
                stuffIds: [LAMP_A, SOFA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        await box.ExecuteAsync(ctx, Ct);

        lamp.Logic.GetState().Should().Be(1);
        ctx.FloorItemStateUpdates.Select(x => x.Item1.Value).Should().Equal(LAMP_A);
    }

    // action.15.R3: the random draw may be the furni's current state (measured 5 of 12 draws on a
    // 2-state lamp); Habbo then sends no state update and "Furni state changes" does not fire.
    // A 1-state furni always draws the state it is in.
    [Fact]
    public async Task A_random_draw_of_the_current_state_changes_nothing()
    {
        var lamp = _room.AddFloorItem(LAMP_A, 3, 3, totalStates: 1);
        var box = _room.AddBox<WiredActionToggleToRandomState>(BOX, 0, 0, "wf_act_toggle_to_rnd");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stuffIds: [LAMP_A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        await box.ExecuteAsync(ctx, Ct);

        lamp.Logic.GetState().Should().Be(0);
        ctx.FloorItemStateUpdates.Should().BeEmpty();
    }

    // action.15.R3 on a 2-state lamp: every update sent is a real change.
    [Fact]
    public async Task Each_random_state_update_is_a_change()
    {
        var lamp = _room.AddFloorItem(LAMP_A, 3, 3);
        var box = _room.AddBox<WiredActionToggleToRandomState>(BOX, 0, 0, "wf_act_toggle_to_rnd");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stuffIds: [LAMP_A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        for (var i = 0; i < 40; i++)
        {
            var before = lamp.Logic.GetState();
            var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

            await box.ExecuteAsync(ctx, Ct);

            ctx.FloorItemStateUpdates.Should().HaveCount(lamp.Logic.GetState() == before ? 0 : 1);
        }
    }

    // action.56.R3: a selector holding two real furni and two temporary copies; Remove
    // Temporary Furni takes only the copies, told to the room in ONE ObjectRemoveMultiple with
    // picker -1 (official client capture 2026-10-24: four ids, pickerId -1). Ours sent one
    // ObjectRemove per copy.
    [Fact]
    public async Task Removing_temporary_furni_tells_the_room_once()
    {
        var furni = _room.Harness.Module<RoomFurniModule>();

        // What the items loader makes of a definition, as TemporaryFurniIdBandTests has it.
        _room.Harness.Fakes.Handlers["CreateFloorItem"] = call =>
        {
            var item = new RoomFloorItem
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                OwnerId = (PlayerId)call.Args[1]!,
                OwnerName = string.Empty,
                Definition = (FurnitureDefinitionSnapshot)call.Args[2]!,
            };

            item.SetExtraData(null);

            return item;
        };
        var lamp = _room.AddFloorItem(LAMP_A, 2, 2);
        _room.AddFloorItem(LAMP_B, 3, 3);

        var copies = new List<IRoomFloorItem>();

        foreach (var x in new[] { 4, 5 })
            copies.Add(
                (
                    await furni.PlaceTemporaryFloorItemAsync(
                        ActionContext.CreateForSystem(1),
                        lamp.Definition,
                        PlayerId.Invalid,
                        x,
                        4,
                        null,
                        Rotation.North,
                        Ct
                    )
                )!
            );

        var box = _room.AddBox<WiredActionRemoveFurni>(BOX, 0, 0, "wf_act_remove_furni");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        var selected = new[] { copies[1].ObjectId.Value, LAMP_A, copies[0].ObjectId.Value, LAMP_B };
        var ctx = new WiredExecutionContext(_room.Harness.Room)
        {
            CancellationToken = Ct,
            SelectorPool = new WiredSelectionSet(),
        };

        foreach (var id in selected)
            ctx.SelectorPool.SelectedFurniIds.Add(id);

        (await box.ExecuteAsync(ctx, Ct)).Should().BeTrue();

        _room.Harness.Fakes.SentToRoom<ObjectRemoveMessageComposer>().Should().BeEmpty();
        var removed = _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMultipleMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        removed
            .ObjectIdsToRemove.Should()
            .Equal(copies[1].ObjectId.Value, copies[0].ObjectId.Value);
        removed.PickerId.Should().Be(PlayerId.Invalid);
        _room.Harness.State.ItemsById.Should().ContainKeys(LAMP_A, LAMP_B);
        _room
            .Harness.State.ItemsById.Keys.Should()
            .NotContain([copies[0].ObjectId, copies[1].ObjectId]);
    }

    // action.54.E9b: Click Configuration saved with [9, 9] is stored as [2, 1], each pulled to
    // the end of its options (official client capture 2026-10-24). Ours refused the save.
    [Theory]
    [InlineData(9, 9, 2, 1)]
    [InlineData(1, 0, 1, 0)]
    public async Task Click_configuration_pulls_its_options_into_range(
        int user,
        int furni,
        int storedUser,
        int storedFurni
    )
    {
        var box = _room.AddBox<WiredActionClickSettings>(BOX, 0, 0, "wf_act_click_conf");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [user, furni],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(storedUser, storedFurni);
    }

    // trigger.4.B1: Furni Is Used picking a stateless chair and a lamp. A player's use of the
    // chair gets no reply at all and does not fire it; a use of the lamp does (official client
    // capture 2026-10-24: chair 3/3 nothing, lamp 2/2 fired). Ours wrote state 0 back to the
    // chair, sent it, and fired the stack.
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task Using_a_furni_without_states_does_nothing(int chairStates, bool fires)
    {
        const int CHAIR = 32;
        const int EFFECT = 2;
        const int AVATAR = 5;

        _room.Enter(AVATAR, 4, 4);
        var chair = _room.AddFloorItem(CHAIR, 4, 5, totalStates: chairStates);
        var lamp = _room.AddFloorItem(LAMP_A, 6, 6);
        _room.AddBox<WiredTriggerHabboChangesItemState>(BOX, 0, 0, "wf_trg_state_changed");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                BOX,
                stuffIds: [CHAIR],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [LAMP_A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var wired = _room.Harness.Module<RoomWiredSystem>();
        var now = 10_000L;

        async Task RunWiredAsync()
        {
            for (var i = 0; i < 10; i++)
            {
                now += 50;
                await wired.ProcessWiredAsync(now, dormant: false, Ct);
            }
        }

        await wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await RunWiredAsync();

        var updatesBefore = _room
            .Harness.Fakes.SentToRoom<ObjectDataUpdateMessageComposer>()
            .Count();

        await _room.Harness.Room.UseItemByIdAsync(_room.ContextFor(AVATAR), CHAIR, Ct);
        await RunWiredAsync();

        lamp.Logic.GetState().Should().Be(fires ? 1 : 0);
        _room
            .Harness.Fakes.SentToRoom<ObjectDataUpdateMessageComposer>()
            .Skip(updatesBefore)
            .Any(x => x.ObjectId == chair.ObjectId)
            .Should()
            .Be(fires);
    }

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
}
