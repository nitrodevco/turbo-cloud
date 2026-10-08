using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "WIRED Effect: Join Team" as its editor saves it: the team, then the kind of team
/// (<c>wiredfurni.params.team_type.0</c> to <c>.2</c>: Wired, Battle Banzai, Freeze). A user clicks
/// a furni and the box puts them in red; another user is already in red, so "the team with the
/// fewest members" would be another colour.
/// </summary>
public sealed class WiredJoinTeamTests
{
    private const int CLICK_ME = 23;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    public WiredJoinTeamTests()
    {
        _room.Enter(5, 1, 1);
        _room.Enter(6, 2, 2);
        _room.AddFloorItem(CLICK_ME, 7, 4);
    }

    [Theory]
    [InlineData(WiredTeamType.Wired, 33)]
    [InlineData(WiredTeamType.BattleBanzai, 33)]
    [InlineData(WiredTeamType.Freeze, 40)]
    public async Task The_user_joins_the_chosen_team_wearing_that_kind_of_team_effect(
        WiredTeamType teamType,
        int redEffect
    )
    {
        (await Game.JoinTeamAsync((PlayerId)106, GameTeamType.Red, Ct)).Should().BeTrue();
        await BuildAsync(teamType);

        await ClickAsync();
        await TickAsync(4);

        Game.GetTeam((PlayerId)105).Should().Be(GameTeamType.Red);
        _room.Avatars[5].EffectId.Should().Be(redEffect);
    }

    private async Task BuildAsync(WiredTeamType teamType)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionJoinTeam>(2, 0, 0, "wf_act_join_team");

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
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)GameTeamType.Red, (int)teamType],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
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
