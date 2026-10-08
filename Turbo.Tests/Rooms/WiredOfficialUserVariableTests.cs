using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The user internal variables as the official client's Creator Tools list and inspect them
/// (captured 2026-10-08, the room owner in their group homeroom): the owner holds
/// <c>@has_rights</c>; <c>@position</c> is (x &lt;&lt; 8) | y (4106 at 16, 10);
/// <c>@favourite_group_id</c> is the group id; the team variables are not held off a team, and
/// <c>@team.type</c> is 0 Battle Banzai, 1 Freeze, 2 Tagging, 3 Swimming, 4 Wired. We had no
/// user <c>@position</c>, <c>@team.type</c> or <c>@favourite_group_id</c>, the owner did not hold
/// <c>@has_rights</c> and <c>@team.color</c> was held, as 0, by everyone.
/// </summary>
public sealed class WiredOfficialUserVariableTests
{
    private const int INDEX = 5;

    private readonly WiredRoom _room = new(20, 20);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_room_owner_has_rights()
    {
        var player = _room.Enter(INDEX, 1, 1);
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));

        RoomHarness.SetMember(info, "RoomId", (RoomId)1);
        RoomHarness.SetMember(info, "OwnerId", player.PlayerId);
        RoomHarness.SetMember(_room.Harness.State, "RoomSnapshot", info);

        Holds(new UserHasRightsVariable(_room.Harness.Room)).Should().BeTrue();
        Holds(new UserIsOwnerVariable(_room.Harness.Room)).Should().BeTrue();
    }

    [Fact]
    public void A_users_position_is_x_and_y_in_one_value()
    {
        _room.Enter(INDEX, 16, 10);

        Read(new UserPositionVariable(_room.Harness.Room)).Should().Be(4106);
    }

    [Fact]
    public void The_favourite_group_is_held_only_by_one_who_has_one()
    {
        var player = _room.Enter(INDEX, 1, 1);
        var variable = new UserFavouriteGroupIdVariable(_room.Harness.Room);

        Holds(variable).Should().BeFalse();

        player.GetType().GetProperty("GuildId")!.SetValue(player, 303029);

        Read(variable).Should().Be(303029);
    }

    [Theory]
    [InlineData(WiredTeamType.BattleBanzai, 0)]
    [InlineData(WiredTeamType.Freeze, 1)]
    [InlineData(WiredTeamType.Wired, 4)]
    public async Task The_team_variables_are_held_only_on_a_team(WiredTeamType kind, int teamType)
    {
        var player = _room.Enter(INDEX, 1, 1);
        var color = new UserTeamColorVariable(_room.Harness.Room);
        var score = new UserTeamScoreVariable(_room.Harness.Room);
        var type = new UserTeamTypeVariable(_room.Harness.Room);

        Holds(color).Should().BeFalse();
        Holds(score).Should().BeFalse();
        Holds(type).Should().BeFalse();

        (
            await _room
                .Harness.Module<RoomGameSystem>()
                .JoinTeamAsync(player.PlayerId, GameTeamType.Blue, kind, Ct)
        )
            .Should()
            .BeTrue();

        Read(color).Should().Be((int)GameTeamType.Blue);
        Read(score).Should().Be(0);
        Read(type).Should().Be(teamType);
        type.GetVarSnapshot().TextConnectors[teamType].Should().NotBeNullOrEmpty();
    }

    private static bool Holds(WiredInternalVariable variable) =>
        variable.TryGetValue(Key(variable), out _);

    private static long Read(WiredInternalVariable variable)
    {
        variable.TryGetValue(Key(variable), out var value).Should().BeTrue();

        return value.Value;
    }

    private static WiredVariableKey Key(WiredInternalVariable variable) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.User, INDEX);
}
