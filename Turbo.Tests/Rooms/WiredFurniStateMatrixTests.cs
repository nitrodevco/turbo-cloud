using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
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
    // compare with (slot 1) another lamp of A's kind. Habbo's default (code 0) is "all of the furni
    // match"; ours always took "any".
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
        const int LAMP_KIND = 500;
        const int CHAIR = 32;

        _room.AddFloorItem(LAMP_A, 3, 3, definitionId: LAMP_KIND);
        _room.AddFloorItem(LAMP_B, 4, 4, definitionId: LAMP_KIND);
        _room.AddFloorItem(CHAIR, 5, 5);

        var condition = negative
            ? _room.AddBox<WiredNegativeConditionItemPerType>(BOX, 0, 0, "wf_cnd_not_stuff_is")
            : _room.AddBox<WiredConditionItemPerType>(BOX, 0, 0, "wf_cnd_stuff_is");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                stuffIds: [LAMP_A, CHAIR],
                stuffIds2: [LAMP_B],
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
