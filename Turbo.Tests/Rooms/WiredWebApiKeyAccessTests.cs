using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Who may see and set a "Variable Global Add-on: Web API"'s keys: the room's owner ("Nobody else
/// can see the contents of this box", <c>wiredfurni.params.web_api.usage_info</c>), and staff only
/// with <c>room.wired.web_api_keys_any</c>. Before, control of every room (<c>room.control.any</c>)
/// was enough to read, generate and save the keys of anyone's room.
/// </summary>
public sealed class WiredWebApiKeyAccessTests
{
    private const int ADDON = 2;
    private const int OWNER = 1;
    private const int STAFF = 2;

    private readonly WiredRoom _room = new(8, 8);
    private string _ownerRead = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredWebApiKeyAccessTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _room.Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
    }

    [Fact]
    public async Task Staff_controlling_every_room_neither_see_nor_set_the_owners_keys()
    {
        await SetUpAsync();
        Enter(STAFF, [PermissionNodes.Room.CONTROL_ANY]);

        (await GenerateAsync(STAFF)).Should().BeNull();
        (await SnapshotAsync(STAFF)).Should().BeEmpty();
        (await SaveAsync(STAFF, string.Empty)).ErrorKey.Should().Be(WiredSaveErrors.GENERIC);
        (await SnapshotAsync(OWNER)).Should().Be(_ownerRead + "\t");
    }

    [Fact]
    public async Task A_player_with_rights_who_may_edit_wired_neither_sees_nor_gets_keys()
    {
        const int WITH_RIGHTS = 3;
        await SetUpAsync();
        Enter(WITH_RIGHTS, []);
        ((ISet<PlayerId>)RoomHarness.GetMember(_room.Harness.State, "PlayerIdsWithRights")!).Add(
            (PlayerId)WITH_RIGHTS
        );
        foreach (var mask in new[] { "WiredModifyPermissionMask", "WiredReadPermissionMask" })
            RoomHarness.SetMember(
                RoomHarness.GetMember(_room.Harness.State, "RoomSnapshot")!,
                mask,
                WiredPermissionFlags.Rights
            );

        (await GenerateAsync(WITH_RIGHTS)).Should().BeNull();
        (await SnapshotAsync(WITH_RIGHTS)).Should().BeEmpty();
    }

    [Fact]
    public async Task Staff_granted_the_keys_node_see_and_set_them()
    {
        await SetUpAsync();
        Enter(
            STAFF,
            [PermissionNodes.Room.CONTROL_ANY, PermissionNodes.Room.WIRED_WEB_API_KEYS_ANY]
        );

        var key = await GenerateAsync(STAFF);

        key.Should().NotBeNull();
        (await SnapshotAsync(STAFF)).Should().Be(_ownerRead + "\t");
        (await SaveAsync(STAFF, key + "\t")).IsSaved.Should().BeTrue();
    }

    private async Task SetUpAsync()
    {
        Enter(OWNER, []);
        _room.AddBox<WiredAddonVariablesWebApi>(ADDON, 6, 6, "wf_xtra_var_web_api");
        _ownerRead = (await GenerateAsync(OWNER))!;
        (await SaveAsync(OWNER, _ownerRead + "\t")).IsSaved.Should().BeTrue();
    }

    private Task<string?> GenerateAsync(int playerId) =>
        _room.Harness.Room.GenerateWiredWebApiKeyAsync(As(playerId), ADDON, true, Ct);

    private async Task<string?> SnapshotAsync(int playerId) =>
        (
            await _room.Harness.Room.GetWiredDataSnapshotByFloorItemIdAsync(As(playerId), ADDON, Ct)
        )?.StringParam;

    private Task<WiredSaveResult> SaveAsync(int playerId, string keys) =>
        _room.Harness.Room.ApplyWiredUpdateAsync(
            As(playerId),
            ADDON,
            new UpdateAddonMessage
            {
                Id = ADDON,
                IntParams = [0],
                StringParam = keys,
                StuffIds = [],
                StuffIds2 = [],
                DefinitionSpecifics = [],
                FurniSources = [],
                PlayerSources = [],
                VariableIds = [],
                TypeSpecifics = [],
            },
            Ct
        );

    private static ActionContext As(int playerId) =>
        ActionContext.CreateForPlayer((PlayerId)playerId, (RoomId)1);

    /// <summary>A player standing in the room with these permission nodes (player 1 owns it).</summary>
    private void Enter(int playerId, string[] nodes)
    {
        var avatar = new RoomPlayerAvatar { ObjectId = playerId, PlayerId = playerId };

        avatar.SetPosition(playerId, 1);
        avatar.SetLogic(_room.Harness.Fakes.Create<IRoomPlayerLogic>(playerId));
        avatar.SetPermissions(ResolvedPermissionsSnapshot.EMPTY with { Granted = [.. nodes] });
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByObjectId")!
        )[playerId] = avatar;
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByPlayerId")!
        )[(PlayerId)playerId] = playerId;
        _room.Map.AddAvatar(avatar, false);
    }
}
