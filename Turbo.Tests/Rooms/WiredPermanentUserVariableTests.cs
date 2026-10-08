using System.Runtime.CompilerServices;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A permanent user variable stays with the player. They are given a new room index every time
/// they enter; the value they held is still theirs when they come back, and is not handed to
/// whoever is given their old index. A shared variable ("Permanent, shared") is permanent too.
/// </summary>
public class WiredPermanentUserVariableTests
{
    private const int BoxId = 7;
    private const int Alice = 42;
    private const int Bob = 43;

    private readonly RoomHarness _room = new();
    private readonly Dictionary<int, int> _playerIdByRoomIndex = [];
    private WiredVariableUser _box = null!;

    public WiredPermanentUserVariableTests()
    {
        _room.Fakes.Handlers["get_PlayerId"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int index
                ? (PlayerId)_playerIdByRoomIndex[index]
                : Fakes.NotHandled;
        _room.Fakes.Handlers["get_ObjectId"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int index
                ? (RoomObjectId)index
                : Fakes.NotHandled;
    }

    [Theory]
    [InlineData(WiredAvailabilityType.Persistent)]
    [InlineData(WiredAvailabilityType.Shared)]
    public async Task Player_keeps_a_permanent_value_after_leaving_and_coming_back(
        WiredAvailabilityType availability
    )
    {
        await PlacePermanentBoxAsync(availability);

        Enter(Alice, roomIndex: 3);
        Assert.True(await _box.GiveValueAsync(Key(3), 10));

        Leave(Alice, roomIndex: 3);
        Enter(Alice, roomIndex: 5);

        Assert.True(_box.TryGetValue(Key(5), out var value));
        Assert.Equal(10, (int)value);
    }

    [Theory]
    [InlineData(WiredAvailabilityType.Persistent)]
    [InlineData(WiredAvailabilityType.Shared)]
    public async Task Next_user_given_the_old_room_index_does_not_inherit_the_value(
        WiredAvailabilityType availability
    )
    {
        await PlacePermanentBoxAsync(availability);

        Enter(Alice, roomIndex: 3);
        Assert.True(await _box.GiveValueAsync(Key(3), 10));

        Leave(Alice, roomIndex: 3);
        Enter(Bob, roomIndex: 3);

        Assert.False(_box.TryGetValue(Key(3), out _));
    }

    private WiredVariableKey Key(int roomIndex) =>
        new(_box.GetVarSnapshot().VariableId, WiredVariableTargetType.User, roomIndex);

    private void Enter(int playerId, int roomIndex)
    {
        var player = _room.Fakes.Create<IRoomPlayer>(roomIndex);

        _playerIdByRoomIndex[roomIndex] = playerId;
        AvatarsByPlayerId()[playerId] = roomIndex;
        AvatarsByObjectId()[roomIndex] = player;
    }

    private void Leave(int playerId, int roomIndex)
    {
        AvatarsByPlayerId().Remove(playerId);
        AvatarsByObjectId().Remove(roomIndex);
    }

    private IDictionary<PlayerId, RoomObjectId> AvatarsByPlayerId() =>
        (IDictionary<PlayerId, RoomObjectId>)
            RoomHarness.GetMember(_room.State, "AvatarsByPlayerId")!;

    private IDictionary<RoomObjectId, IRoomAvatar> AvatarsByObjectId() =>
        (IDictionary<RoomObjectId, IRoomAvatar>)
            RoomHarness.GetMember(_room.State, "AvatarsByObjectId")!;

    private async Task PlacePermanentBoxAsync(WiredAvailabilityType availability)
    {
        RoomHarness.SetMember(_room.State, "IsRightsLoaded", true);

        // The wired permission check reads the room's masks; the owner passes whatever they are.
        var info = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(info, "RoomId", (RoomId)1);
        RoomHarness.SetMember(info, "OwnerId", (PlayerId)1);
        RoomHarness.SetMember(_room.State, "RoomSnapshot", info);

        var item = _room.CreateFloorItem(
            BoxId,
            2,
            2,
            Altitude.Zero,
            name: "wf_var_user",
            logic: "wf_var_user",
            createLogic: (stuffDataFactory, ctx) =>
                new WiredVariableUser(_room.Fakes.Create<IGrainFactory>(), stuffDataFactory, ctx)
        );
        _room.AddToRoom(item);

        Assert.True(
            await _room.Room.ApplyWiredUpdateAsync(
                ActionContext.CreateForSystem(1),
                BoxId,
                new UpdateWiredMessage
                {
                    Id = BoxId,
                    IntParams = [(int)availability, 1],
                    StringParam = "score",
                    StuffIds = [],
                    StuffIds2 = [],
                    DefinitionSpecifics = [],
                    FurniSources = [],
                    PlayerSources = [],
                    VariableIds = [],
                    TypeSpecifics = [],
                },
                CancellationToken.None
            )
        );

        _box = (WiredVariableUser)item.Logic;
        await _box.LoadWiredAsync(CancellationToken.None);
    }
}
