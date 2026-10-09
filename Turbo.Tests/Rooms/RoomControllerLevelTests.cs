using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's controller level for its owner: Owner for a plain player, Moderator for a staff
/// member holding <c>room.control.any</c>, whose own room is no exception. Before, ownership
/// answered first, so a staff member could not save the boxes only staff may save (Give
/// Reward, Achievement Enabler) in their own room, while they could in anyone else's.
/// </summary>
public sealed class RoomControllerLevelTests
{
    private const int OWNER_OBJECT = 1;
    private const int ENABLER = 2;

    private readonly WiredRoom _room = new(8, 8);

    public RoomControllerLevelTests()
    {
        // A denied node is looked up in the registry, as in the hotel.
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _room.Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
    }

    private RoomSecurityModule Security => _room.Harness.Module<RoomSecurityModule>();

    [Fact]
    public async Task The_owner_of_a_room_is_its_owner()
    {
        EnterOwner([]);

        (await Security.GetControllerLevelAsync((PlayerId)1)).Should().Be(RoomControllerType.Owner);
    }

    [Fact]
    public async Task A_staff_member_controlling_every_room_is_moderator_in_their_own_room_too()
    {
        EnterOwner([PermissionNodes.Room.CONTROL_ANY]);

        (await Security.GetControllerLevelAsync((PlayerId)1))
            .Should()
            .Be(RoomControllerType.Moderator);
    }

    [Fact]
    public async Task A_staff_member_can_save_a_moderator_level_box_in_their_own_room()
    {
        EnterOwner([PermissionNodes.Room.CONTROL_ANY]);
        _room.AddBox<WiredAddonAchievementEnabler>(ENABLER, 6, 6, "wf_xtra_achievement_enabler");

        var result = await _room.Harness.Room.ApplyWiredUpdateAsync(
            ActionContext.CreateForPlayer((PlayerId)1, (RoomId)1),
            ENABLER,
            new UpdateAddonMessage
            {
                Id = ENABLER,
                IntParams = [],
                StringParam = "Lap",
                StuffIds = [],
                StuffIds2 = [],
                DefinitionSpecifics = [],
                FurniSources = [],
                PlayerSources = [],
                VariableIds = [],
                TypeSpecifics = [],
            },
            TestContext.Current.CancellationToken
        );

        result.IsSaved.Should().BeTrue();
    }

    /// <summary>The room's owner (player 1) standing in it with these permission nodes.</summary>
    private void EnterOwner(string[] nodes)
    {
        var avatar = new RoomPlayerAvatar { ObjectId = OWNER_OBJECT, PlayerId = 1 };

        avatar.SetPosition(1, 1);
        avatar.SetLogic(_room.Harness.Fakes.Create<IRoomPlayerLogic>(OWNER_OBJECT));
        avatar.SetPermissions(ResolvedPermissionsSnapshot.EMPTY with { Granted = [.. nodes] });
        (
            (System.Collections.Generic.IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByObjectId")!
        )[OWNER_OBJECT] = avatar;
        (
            (System.Collections.Generic.IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByPlayerId")!
        )[(PlayerId)1] = OWNER_OBJECT;
        _room.Map.AddAvatar(avatar, false);
    }
}
