using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired.Variables;
using Turbo.Rooms.Wired.Variables.Room;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The global internal variables the official client's Creator Tools list (2026-10-08) and this
/// hotel did not have: <c>@teams.&lt;colour&gt;.score</c> (writable) and <c>.size</c> (read
/// only), <c>@room_id</c> and <c>@group_id</c>.
/// </summary>
public sealed class WiredOfficialGlobalVariableTests
{
    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    [Fact]
    public async Task A_team_score_is_read_and_written()
    {
        var blue = new RoomTeamBlueScoreVariable(_room.Harness.Room);
        var red = new RoomTeamRedScoreVariable(_room.Harness.Room);

        blue.GetVarSnapshot().VariableName.Should().Be("@teams.blue.score");
        Read(blue).Should().Be(0);

        (await blue.SetValueAsync(null!, Key(blue), 12)).Should().BeTrue();

        Game.GetScore(GameTeamType.Blue).Should().Be(12);
        Read(blue).Should().Be(12);
        Read(red).Should().Be(0);
    }

    [Fact]
    public async Task A_team_size_counts_who_is_on_it()
    {
        var size = new RoomTeamGreenSizeVariable(_room.Harness.Room);
        var one = _room.Enter(5, 1, 1);
        var two = _room.Enter(6, 2, 2);

        size.GetVarSnapshot().VariableName.Should().Be("@teams.green.size");
        Read(size).Should().Be(0);

        await Game.JoinTeamAsync(one.PlayerId, GameTeamType.Green, Ct);
        await Game.JoinTeamAsync(two.PlayerId, GameTeamType.Green, Ct);

        Read(size).Should().Be(2);
    }

    [Fact]
    public void The_room_and_its_group_are_read()
    {
        var roomId = new RoomIdVariable(_room.Harness.Room);
        var groupId = new RoomGroupIdVariable(_room.Harness.Room);
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));

        RoomHarness.SetMember(info, "RoomId", (RoomId)77);
        RoomHarness.SetMember(_room.Harness.State, "RoomSnapshot", info);

        Read(roomId).Should().Be(77);
        groupId.TryGetValue(Key(groupId), out _).Should().BeFalse();

        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));
        RoomHarness.SetMember(guild, "GuildId", (Turbo.Primitives.Guilds.GuildId)303029);
        RoomHarness.SetMember(info, "Guild", guild);

        Read(groupId).Should().Be(303029);
    }

    private static long Read(WiredInternalVariable variable)
    {
        variable.TryGetValue(Key(variable), out var value).Should().BeTrue();

        return value.Value;
    }

    private static WiredVariableKey Key(WiredInternalVariable variable) =>
        new(variable.GetVarSnapshot().VariableId, WiredVariableTargetType.Global, 0);
}
