using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room settings' Rights tab asks for the ban list, and Flash asks again whenever it arrives
/// empty. Habbo (hhus, 2026-10-12) answered the first request with an empty list and let the two
/// repeats 0.1 s behind it go unanswered; Turbo answered an empty list with nothing at all.
/// </summary>
public sealed class RoomBanListRepliesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task an_empty_ban_list_is_answered_once_and_its_quick_repeats_are_let_go()
    {
        var room = new WiredRoom(4, 4).Harness;
        RoomHarness.SetMember(
            RoomHarness.GetMember(room.State, "RoomSnapshot")!,
            "ModSettings",
            ModSettingsSnapshot.OwnerOnly
        );
        var bans = room.Module<RoomModerationModule>();
        var owner = ActionContext.CreateForPlayer((PlayerId)1, (RoomId)1);

        var first = await bans.GetBannedPlayersAsync(owner, Ct);
        var repeat = await bans.GetBannedPlayersAsync(owner, Ct);

        first.Should().NotBeNull();
        first!.Value.Should().BeEmpty();
        repeat.Should().BeNull();
    }
}
