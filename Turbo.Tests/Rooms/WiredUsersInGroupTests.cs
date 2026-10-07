using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Users in group" saved with no group is the editor's "Current group" (<c>grouptype.0</c>): the
/// room's own group. One user wears group 77's badge, another group 88's; the room's group is 77.
/// </summary>
public sealed class WiredUsersInGroupTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task Current_group_picks_only_the_rooms_group()
    {
        _room.Enter(5, 1, 1).SetFavouriteGuild(77, GuildMembershipStatus.Member, "Ours");
        _room.Enter(6, 2, 2).SetFavouriteGuild(88, GuildMembershipStatus.Member, "Theirs");

        var guild = (GuildSummarySnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(GuildSummarySnapshot));
        RoomHarness.SetMember(guild, nameof(GuildSummarySnapshot.GuildId), new GuildId(77));
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(info, "RoomId", (RoomId)1);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)1);
        RoomHarness.SetMember(info, "Guild", guild);
        RoomHarness.SetMember(_room.Harness.State, "RoomSnapshot", info);

        var selector = _room.AddBox<WiredSelectorEntitiesInGroup>(2, 0, 0, "wf_slc_users_group");
        (await _room.SaveAsync<UpdateSelectorMessage>(2, definitionSpecifics: [false, false]))
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
        };

        var selected = await selector.SelectAsync(ctx, TestContext.Current.CancellationToken);

        selected.SelectedAvatarIds.Select(x => x.Value).Should().BeEquivalentTo([5]);
    }
}
