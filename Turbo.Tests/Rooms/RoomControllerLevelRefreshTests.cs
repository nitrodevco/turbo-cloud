using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player's level refreshed while they stand in the room (rights given or taken, a group
/// homeroom's member joining, leaving or made admin). Habbo answers YouAreController or
/// YouAreNotController only when the level moved, then WiredPermissions and
/// RaidProtectionCapability every time (official client captures: 2 joins and 2 kicks without
/// rights told neither, admin made and unmade and rights given 3 of 3 told; raid protection after
/// the wired permissions 7 of 7, true for every level with rights). Turbo told the controller
/// level on every refresh, never the raid protection, and only owners could manage it.
/// </summary>
public sealed class RoomControllerLevelRefreshTests
{
    private const int OBJECT = 5;
    private static readonly PlayerId Player = 100 + OBJECT;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomControllerLevelRefreshTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _room.Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
        _room.Enter(OBJECT, 2, 2).SetPermissions(ResolvedPermissionsSnapshot.EMPTY);
    }

    private RoomSecurityModule Security => _room.Harness.Module<RoomSecurityModule>();

    [Fact]
    public async Task A_refresh_that_leaves_the_level_alone_tells_no_controller_level()
    {
        await Security.TakeEntryControllerLevelAsync(Player);

        await Security.RefreshControllerLevelForPlayerAsync(Player, Ct);

        _room.Harness.Fakes.Log.Of("OnControllerLevelUpdatedAsync").Should().BeEmpty();
        Sent()
            .Select(x => x.GetType())
            .Should()
            .Equal(
                typeof(WiredPermissionsEventMessageComposer),
                typeof(RaidProtectionCapabilityMessageComposer)
            );
        Sent()
            .OfType<RaidProtectionCapabilityMessageComposer>()
            .Single()
            .CanManage.Should()
            .BeFalse();
    }

    [Fact]
    public async Task Rights_given_tell_the_level_and_that_raid_protection_may_be_managed()
    {
        await Security.TakeEntryControllerLevelAsync(Player);
        ((ISet<PlayerId>)RoomHarness.GetMember(_room.Harness.State, "PlayerIdsWithRights")!).Add(
            Player
        );

        await Security.RefreshControllerLevelForPlayerAsync(Player, Ct);

        _room
            .Harness.Fakes.Log.Of("OnControllerLevelUpdatedAsync")
            .Should()
            .ContainSingle()
            .Which.Args[1]
            .Should()
            .Be(RoomControllerType.Rights);
        Sent()
            .Select(x => x.GetType())
            .Should()
            .Equal(
                typeof(WiredPermissionsEventMessageComposer),
                typeof(RaidProtectionCapabilityMessageComposer)
            );
        Sent()
            .OfType<RaidProtectionCapabilityMessageComposer>()
            .Single()
            .CanManage.Should()
            .BeTrue();
    }

    private List<object> Sent() =>
        [.. _room.Harness.Fakes.Log.Of("SendComposerAsync").Select(x => x.Args[0]!)];
}
