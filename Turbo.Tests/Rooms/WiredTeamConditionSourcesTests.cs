using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Triggerer's team" on Team Is Winning and Team Has Score is the team of the users in the
/// box's user slot: the triggering user by default, or a selector's or a signal's users
/// (official form contracts: users [0,200,201], default 0).
/// </summary>
public sealed class WiredTeamConditionSourcesTests
{
    private const int TRIGGERER = 5;
    private const int BLUE_PLAYER = 6;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    [Theory]
    [InlineData(WiredPlayerSourceType.TriggeredUser, false)]
    [InlineData(WiredPlayerSourceType.SelectorUsers, true)]
    public async Task The_triggerers_team_is_the_team_of_the_user_slot(
        WiredPlayerSourceType source,
        bool passes
    )
    {
        _room.Enter(TRIGGERER, 1, 1);
        _room.Enter(BLUE_PLAYER, 2, 2);
        (await Game.JoinTeamAsync((PlayerId)(100 + BLUE_PLAYER), GameTeamType.Blue, Ct))
            .Should()
            .BeTrue();

        var condition = _room.AddBox<WiredConditionTeamHasScore>(1, 0, 0, "wf_cnd_team_has_score");

        // Triggerer's team, 0 points, equal to.
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                1,
                intParams: [0, 0, 1],
                playerSources: [[source]]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
            Selected = new WiredSelectionSet { SelectedAvatarIds = { TRIGGERER } },
            SelectorPool = new WiredSelectionSet { SelectedAvatarIds = { BLUE_PLAYER } },
        };

        condition.Evaluate(ctx).Should().Be(passes);
    }
}
