using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The user conditions Habbo opens with the users' quantifier radio (quantifierType 2,
/// Fixtures/official-wired-editor-opens.json) count their users as the radio says: "all" (code
/// 0) needs every user to match, "any" (1) one of them. Measured with two users on Triggerer
/// On Furni (wired matrix condition.2.R1 / R5) and with none on Actor In Team (condition.6.R2);
/// that Has Hand Item counts the same way is inference. User 1 holds hand item 2, user 2 nothing.
/// </summary>
public sealed class WiredUserQuantifierTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [Trait("evidence", "inferred")]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task Has_hand_item_counts_the_users_as_its_radio_says(int quantifier, bool passes)
    {
        _room.Enter(1, 1, 1).SetHandItem(2);
        _room.Enter(2, 2, 2);
        var condition = _room.AddBox<WiredConditionHabboHasHanditem>(
            BOX,
            0,
            0,
            "wf_cnd_has_handitem"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [2],
                definitionSpecifics: [quantifier]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
        ctx.Selected.SelectedAvatarIds.UnionWith([1, 2]);

        condition.Evaluate(ctx).Should().Be(passes);
    }
}
