using System.Collections;
using System.Runtime.CompilerServices;
using EvalHarness;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests: a player whose avatar is already in the room (reloading it) must
/// not be refused for capacity or sent back to the door; bans still apply. Driven through the
/// room grain's CheckEntryAccessAsync, which the entry flow calls.
/// </summary>
public class ReentryAccessTests
{
    private const int Owner = 100;
    private const int Inside = 5;
    private const int Outside = 6;

    private static RoomHarness Room(int playersMax, RoomDoorModeType door, int population, string password = "")
    {
        var h = new RoomHarness();
        var snap = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(snap, "RoomId", (Turbo.Primitives.Rooms.RoomId)1);
        RoomHarness.SetMember(snap, "OwnerId", (PlayerId)Owner);
        RoomHarness.SetMember(snap, "PlayersMax", playersMax);
        RoomHarness.SetMember(snap, "DoorMode", door);
        RoomHarness.SetMember(snap, "Password", password);
        RoomHarness.SetMember(snap, "Name", "eval");
        RoomHarness.SetMember(h.State, "RoomSnapshot", snap);

        h.Fakes.Handlers["GetRoomPopulationAsync"] = _ => Task.FromResult(population);
        h.Fakes.Handlers["GetIsGroupRoomAsync"] = _ => Task.FromResult(false);

        // The player already standing in the room.
        var byPlayer = (IDictionary)RoomHarness.GetMember(h.State, "AvatarsByPlayerId")!;
        var idType = byPlayer.GetType().GetGenericArguments()[1];
        byPlayer[(PlayerId)Inside] = Activator.CreateInstance(idType, 77) ?? 77;
        return h;
    }

    private static Task<RoomEntryAccessType> Check(RoomHarness h, int playerId, string? password = null) =>
        h.Room.CheckEntryAccessAsync(playerId, password, false, CancellationToken.None);

    [Fact]
    public async Task FullRoom_PlayerAlreadyInside_IsAllowed()
    {
        var h = Room(playersMax: 10, RoomDoorModeType.Open, population: 10);
        Assert.Equal(RoomEntryAccessType.Allowed, await Check(h, Inside));
    }

    [Fact]
    public async Task LockedRoom_PlayerAlreadyInside_IsNotSentToDoorbell()
    {
        var h = Room(playersMax: 0, RoomDoorModeType.Locked, population: 3);
        Assert.Equal(RoomEntryAccessType.Allowed, await Check(h, Inside));
    }

    [Fact]
    public async Task PasswordRoom_PlayerAlreadyInside_NeedsNoPassword()
    {
        var h = Room(playersMax: 0, RoomDoorModeType.Password, population: 3, password: "secret");
        Assert.Equal(RoomEntryAccessType.Allowed, await Check(h, Inside, null));
    }

    [Fact]
    public async Task BannedWhileInside_IsStillBanned()
    {
        var h = Room(playersMax: 10, RoomDoorModeType.Open, population: 10);
        var bans = (IDictionary)RoomHarness.GetMember(h.State, "BannedUntilByPlayerId")!;
        bans[(PlayerId)Inside] = DateTime.UtcNow.AddHours(1);
        Assert.Equal(RoomEntryAccessType.Banned, await Check(h, Inside));
    }

    [Fact]
    public async Task FullRoom_NewPlayer_IsFull()
    {
        var h = Room(playersMax: 10, RoomDoorModeType.Open, population: 10);
        Assert.Equal(RoomEntryAccessType.Full, await Check(h, Outside));
    }

    [Fact]
    public async Task LockedRoom_NewPlayer_RingsDoorbell()
    {
        var h = Room(playersMax: 0, RoomDoorModeType.Locked, population: 3);
        Assert.Equal(RoomEntryAccessType.Doorbell, await Check(h, Outside));
    }

    [Fact]
    public async Task Owner_IsAllowedIntoFullLockedRoom()
    {
        var h = Room(playersMax: 10, RoomDoorModeType.Locked, population: 10);
        Assert.Equal(RoomEntryAccessType.Allowed, await Check(h, Owner));
    }
}
