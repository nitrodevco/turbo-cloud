using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Triggerer Matches has two user slots: the users to match and the users to compare with
/// (sources.users.title.match.0 / .1; official form contract users [0,200,201] default 0 and
/// [0,101,200,201] default 101, the user named in the box).
/// </summary>
public sealed class WiredTriggererMatchesSourcesTests
{
    private const int TRIGGERER = 5;
    private const int OTHER = 6;

    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(TRIGGERER, true)]
    [InlineData(OTHER, false)]
    public async Task The_triggerer_matches_when_it_is_among_the_users_to_compare_with(
        int selected,
        bool passes
    )
    {
        _room.Enter(TRIGGERER, 1, 1);
        _room.Enter(OTHER, 2, 2);

        var condition = _room.AddBox<WiredConditionExecutorMatches>(
            1,
            0,
            0,
            "wf_cnd_triggerer_match"
        );

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                1,
                intParams: [1],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        condition.Evaluate(Context(selected)).Should().Be(passes);
    }

    [Fact]
    public async Task By_default_the_user_named_in_the_box_is_compared()
    {
        _room.Enter(TRIGGERER, 1, 1);

        var condition = _room.AddBox<WiredConditionExecutorMatches>(
            1,
            0,
            0,
            "wf_cnd_triggerer_match"
        );

        (await _room.SaveAsync<UpdateConditionMessage>(1, intParams: [1], stringParam: "nobody"))
            .Should()
            .BeTrue();

        condition.GetPlayerSources()[1].Should().Equal(WiredPlayerSourceType.UserByName);
        condition.Evaluate(Context(TRIGGERER)).Should().BeFalse();
    }

    private WiredProcessingContext Context(int selected) =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
            Selected = new WiredSelectionSet { SelectedAvatarIds = { TRIGGERER } },
            SelectorPool = new WiredSelectionSet { SelectedAvatarIds = { selected } },
        };
}
