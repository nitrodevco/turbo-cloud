using FluentAssertions;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room.Engine;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>RemoveItem</c> destroys a wall item for good. Flash sends it from the post-it's bin and the
/// photo viewer's remove button only, for a wall item only (<c>RoomEngine.deleteRoomObject</c>
/// acts on category 20 alone), and offers both only from controller level 4 up: the photo's
/// <c>hasRightsToRemove</c> (<c>roomControllerLevel &gt;= 4</c>,
/// <c>ui/handler/ExternalImageWidgetHandler.as:99-102</c>) and the post-it's <c>isRoomOwner ||
/// isAnyRoomController</c> (<c>ui/handler/FurnitureStickieWidgetHandler.as:81</c>), build
/// WIN63-202610201032. The server asked only for rights or the item's ownership, and of any item,
/// so a rights holder destroyed the owner's furni, floor items too. What Habbo's server refuses is
/// not recorded: the rule is the client's.
/// </summary>
[Trait("evidence", "inferred")]
public sealed class RemoveWallItemTests
{
    // The room is player 1's; player 102 stands in it with rights, player 107 is staff.
    private const int OWNER = 1;
    private const int BUILDER = 2;
    private const int STAFF = 7;
    private const int PHOTO = 500;
    private const int TABLE = 501;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RemoveWallItemTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _room.Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;

        _room.Enter(OWNER, 1, 1, playerId: 1);
        _room.Enter(BUILDER, 2, 1);
        _room.Harness.State.PlayerIdsWithRights.Add(100 + BUILDER);
        _room
            .Enter(STAFF, 3, 1)
            .SetPermissions(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = [PermissionNodes.Room.CONTROL_ANY],
                }
            );
        _room.AddWallItem(PHOTO);
        _room.AddFloorItem(TABLE, 4, 4);
    }

    [Fact]
    public async Task A_rights_holder_cannot_destroy_the_owners_photo()
    {
        (await RemoveAsync(BUILDER, PHOTO)).Should().BeFalse();
    }

    [Fact]
    public async Task Floor_furni_is_never_destroyed_by_it()
    {
        (await RemoveAsync(OWNER, TABLE)).Should().BeFalse();
    }

    [Theory]
    [InlineData(OWNER)]
    [InlineData(STAFF)]
    public async Task The_rooms_owner_and_staff_controlling_every_room_destroy_a_wall_item(
        int remover
    )
    {
        (await RemoveAsync(remover, PHOTO)).Should().BeTrue();
    }

    // What the user's client sends: RemoveItem with the item's id, through the packet's handler.
    private async Task<bool> RemoveAsync(int objectId, int itemId)
    {
        var fakes = _room.Harness.Fakes;
        fakes.Instances[(typeof(IRoomGrain), 1L)] = _room.Harness.Room;

        await new RemoveItemMessageHandler(fakes.Create<IGrainFactory>()).HandleAsync(
            new RemoveItemMessage { ItemId = itemId },
            new MessageContext(
                fakes.Create<ISessionContext>(),
                _room.Avatars[objectId].PlayerId,
                1
            ),
            Ct
        );

        return !InRoom(itemId);
    }

    private bool InRoom(int itemId) => _room.Harness.State.ItemsById.ContainsKey(itemId);
}
