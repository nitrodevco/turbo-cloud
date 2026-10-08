using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Triggerer is in group" saved with no group is the editor's "Current group"
/// (<c>wiredfurni.params.grouptype.0</c>): the room's own group, not any group. The user who
/// clicks wears group 77's badge; the stack puts them in the red team when the condition holds.
/// </summary>
public sealed class WiredGroupMemberConditionTests
{
    private const int CLICK_ME = 23;
    private const int USERS_GROUP = 77;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    public WiredGroupMemberConditionTests()
    {
        _room
            .Enter(5, 1, 1)
            .SetFavouriteGuild(USERS_GROUP, GuildMembershipStatus.Member, "Testers");
        _room.AddFloorItem(CLICK_ME, 7, 4);
    }

    [Fact]
    public async Task Current_group_holds_for_a_member_of_the_rooms_group()
    {
        await BuildAsync(roomGroup: USERS_GROUP);

        await ClickAsync();
        await TickAsync(4);

        Game.GetTeam((PlayerId)105).Should().Be(GameTeamType.Red);
    }

    [Fact]
    public async Task Current_group_does_not_hold_for_a_member_of_another_group()
    {
        await BuildAsync(roomGroup: 99);

        await ClickAsync();
        await TickAsync(4);

        Game.GetTeam((PlayerId)105).Should().Be(GameTeamType.None);
    }

    private async Task BuildAsync(int roomGroup)
    {
        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));
        RoomHarness.SetMember(guild, nameof(GuildSummarySnapshot.GuildId), new GuildId(roomGroup));

        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(info, "RoomId", (RoomId)1);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)1);
        RoomHarness.SetMember(info, "Guild", guild);
        RoomHarness.SetMember(_room.Harness.State, "RoomSnapshot", info);

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredConditionGroupMember>(2, 0, 0, "wf_cnd_actor_in_group");
        _room.AddBox<WiredActionJoinTeam>(3, 0, 0, "wf_act_join_team");

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
        // No group: "Current group".
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                2,
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [(int)GameTeamType.Red, (int)WiredTeamType.Wired],
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
