using System.Collections;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A variable box takes every value it stored with it, so Habbo lets only the room's owner and
/// the box's own owner pick it up: a room admin who could pick up borrowed furni was refused 8
/// variables with <c>furni_placement_error</c> <c>room.error.variable_permissions</c> ("Only the
/// room owner and wired variable owner can pick up this furniture", official hotel texts
/// 2026-10-12). Ours let a group admin pick up anybody's variable, wiping its values.
/// </summary>
public sealed class VariablePickupPermissionTests
{
    private const int OWNER = 1;
    private const int ADMIN = 5;
    private const int MEMBER = 6;
    private const int VARIABLE = 40;
    private const int CHAIR = 41;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public VariablePickupPermissionTests()
    {
        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));

        RoomHarness.SetMember(guild, "GuildId", (GuildId)9);
        _room.Harness.Fakes.Handlers["GetGuildOfRoomAsync"] = _ =>
            Task.FromResult<GuildSummarySnapshot?>(guild);
        _room.Harness.Fakes.Handlers["GetMemberRankAsync"] = call =>
            Task.FromResult<GuildMemberRank?>(
                (PlayerId)call.Args[0]! == ADMIN ? GuildMemberRank.Admin : GuildMemberRank.Member
            );
    }

    [Fact]
    public async Task A_group_admin_is_refused_the_room_owners_variable_and_told_why()
    {
        _room.AddBox<WiredVariableUser>(VARIABLE, 3, 3, "wf_var_user");

        (await _room.Harness.Room.RemoveItemByIdAsync(As(ADMIN), VARIABLE, true, Ct))
            .Should()
            .BeFalse();

        ItemsById.Contains((RoomObjectId)VARIABLE).Should().BeTrue();
        SentTo<NotificationDialogMessageComposer>(ADMIN)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<NotificationDialogMessageComposer>(n =>
                n.NotificationType == "furni_placement_error"
                && n.Parameters["message"] == "${room.error.variable_permissions}"
            );
    }

    [Fact]
    public async Task A_group_admin_still_picks_up_ordinary_furni()
    {
        _room.AddFloorItem(CHAIR, 3, 3);

        (await _room.Harness.Room.RemoveItemByIdAsync(As(ADMIN), CHAIR, true, Ct))
            .Should()
            .BeTrue();

        ItemsById.Contains((RoomObjectId)CHAIR).Should().BeFalse();
        SentTo<NotificationDialogMessageComposer>(ADMIN).Should().BeEmpty();
    }

    [Fact]
    public async Task The_variables_own_owner_picks_it_up_after_confirming()
    {
        _room.AddBox<WiredVariableUser>(VARIABLE, 3, 3, "wf_var_user");
        _room.FloorItem(VARIABLE).SetOwnerId(ADMIN);

        (await _room.Harness.Room.RemoveItemByIdAsync(As(ADMIN), VARIABLE, false, Ct))
            .Should()
            .BeFalse();
        SentTo<ObjectRemoveConfirmMessageComposer>(ADMIN).Should().ContainSingle();

        (await _room.Harness.Room.RemoveItemByIdAsync(As(ADMIN), VARIABLE, true, Ct))
            .Should()
            .BeTrue();
        ItemsById.Contains((RoomObjectId)VARIABLE).Should().BeFalse();
        SentTo<NotificationDialogMessageComposer>(ADMIN).Should().BeEmpty();
    }

    [Fact]
    public async Task The_room_owner_picks_up_anybodys_variable()
    {
        _room.AddBox<WiredVariableUser>(VARIABLE, 3, 3, "wf_var_user");
        _room.FloorItem(VARIABLE).SetOwnerId(ADMIN);

        (await _room.Harness.Room.RemoveItemByIdAsync(As(OWNER), VARIABLE, true, Ct))
            .Should()
            .BeTrue();

        ItemsById.Contains((RoomObjectId)VARIABLE).Should().BeFalse();
    }

    [Fact]
    public async Task Ejecting_everything_leaves_other_players_variables_to_a_group_admin()
    {
        _room.AddBox<WiredVariableUser>(VARIABLE, 3, 3, "wf_var_user");
        _room.AddFloorItem(CHAIR, 4, 4);

        foreach (var id in new[] { VARIABLE, CHAIR })
            _room.FloorItem(id).SetOwnerId(MEMBER);

        (await _room.Harness.Module<RoomActionModule>().EjectAllAsync(As(ADMIN), Ct))
            .Should()
            .Be(1);

        ItemsById.Contains((RoomObjectId)VARIABLE).Should().BeTrue();
        ItemsById.Contains((RoomObjectId)CHAIR).Should().BeFalse();
    }

    private static ActionContext As(int playerId) => ActionContext.CreateForPlayer(playerId, 1);

    private IDictionary ItemsById =>
        (IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!;

    private IEnumerable<T> SentTo<T>(int playerId) =>
        _room
            .Harness.Fakes.Log.Of("SendComposerAsync")
            .Where(c =>
                c.Interface == typeof(IPlayerPresenceGrain) && Equals(c.Key, (long)playerId)
            )
            .Select(c => c.Args[0])
            .OfType<T>();
}
