using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Wired.Variables.User;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>@is_group_admin</c>: the Wired Faculty tutorial "How to do an AUTO PRIZE COUNTER?" lets
/// only the admins of an open group host, with "Has variable" on it for the triggering user. It
/// did not exist.
/// </summary>
public sealed class WiredGroupAdminVariableTests
{
    private const int ADMIN = 5;
    private const int MEMBER = 6;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task Only_the_groups_admins_hold_it()
    {
        var admin = _room.Enter(ADMIN, 1, 1);
        var member = _room.Enter(MEMBER, 2, 2);
        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));

        RoomHarness.SetMember(guild, "GuildId", (GuildId)9);
        RoomHarness.SetMember(guild, "RightsLevel", GuildRightsLevel.Members);
        _room.Harness.Fakes.Handlers["GetGuildOfRoomAsync"] = _ =>
            Task.FromResult<GuildSummarySnapshot?>(guild);
        _room.Harness.Fakes.Handlers["GetMemberRankAsync"] = call =>
            Task.FromResult<GuildMemberRank?>(
                (PlayerId)call.Args[0]! == admin.PlayerId
                    ? GuildMemberRank.Admin
                    : GuildMemberRank.Member
            );

        // What the room works out for every player who comes in to a group homeroom.
        var security = _room.Harness.Module<RoomSecurityModule>();
        var groupLevel = typeof(RoomSecurityModule).GetMethod(
            "GetGroupLevelAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;

        foreach (var player in new[] { admin.PlayerId, member.PlayerId })
            await (Task)groupLevel.Invoke(security, [guild, player])!;

        var variable = new UserIsGroupAdminVariable(_room.Harness.Room);
        var id = variable.GetVarSnapshot().VariableId;

        variable
            .TryGetValue(new WiredVariableKey(id, WiredVariableTargetType.User, ADMIN), out _)
            .Should()
            .BeTrue();
        variable
            .TryGetValue(new WiredVariableKey(id, WiredVariableTargetType.User, MEMBER), out _)
            .Should()
            .BeFalse();
    }
}
