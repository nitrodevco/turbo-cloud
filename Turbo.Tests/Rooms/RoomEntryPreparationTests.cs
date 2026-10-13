using Turbo.Primitives.Action;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

public class RoomEntryPreparationTests
{
    [Theory]
    [InlineData(RoomEntryAccessType.Banned)]
    [InlineData(RoomEntryAccessType.Full)]
    [InlineData(RoomEntryAccessType.PasswordRequired)]
    [InlineData(RoomEntryAccessType.InvalidPassword)]
    [InlineData(RoomEntryAccessType.Closed)]
    [InlineData(RoomEntryAccessType.HiddenByBuildersClub)]
    public async Task RejectedEntry_KeepsPacketOrderWithoutActivatingRoom(
        RoomEntryAccessType access
    )
    {
        var harness = CreateHarness(access);

        var packets = await OpenRoomAsync(harness);

        string[] expected = access switch
        {
            RoomEntryAccessType.PasswordRequired or RoomEntryAccessType.InvalidPassword =>
            [
                "OpenConnectionMessageComposer",
                "GenericErrorMessageComposer",
                "CloseConnectionMessageComposer",
            ],
            RoomEntryAccessType.HiddenByBuildersClub =>
            [
                "OpenConnectionMessageComposer",
                "NotificationDialogMessageComposer",
                "CantConnectMessageComposer",
                "CloseConnectionMessageComposer",
            ],
            _ =>
            [
                "OpenConnectionMessageComposer",
                "CantConnectMessageComposer",
                "CloseConnectionMessageComposer",
            ],
        };

        Assert.Equal(
            expected.Select(PacketHarness.Outgoing),
            packets.Select(packet => (int)packet.Header)
        );
        Assert.Empty(harness.Fakes.Log.Of("EnsureRoomActiveAsync"));
    }

    [Fact]
    public async Task ApprovedEntry_ActivatesBeforeRequestingEntryView()
    {
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["GetEntryViewAsync"] = _ =>
            Task.FromException<RoomEntryViewSnapshot>(
                new InvalidOperationException("view sentinel")
            );

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OpenRoomAsync(harness)
        );

        Assert.Equal("view sentinel", error.Message);
        var roomCalls = harness
            .Fakes.Log.Calls.Where(call =>
                call.Method
                    is "CheckEntryAccessAsync"
                        or "EnsureRoomActiveAsync"
                        or "GetEntryViewAsync"
            )
            .Select(call => call.Method)
            .ToArray();
        Assert.Equal(
            [
                "CheckEntryAccessAsync",
                "EnsureRoomActiveAsync",
                "CheckEntryAccessAsync",
                "GetEntryViewAsync",
            ],
            roomCalls
        );
    }

    [Fact]
    public async Task ApprovedEntry_WhenActivationFailsDoesNotOpenOrJoinRoom()
    {
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["EnsureRoomActiveAsync"] = _ =>
            Task.FromException(new InvalidOperationException("load sentinel"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OpenRoomAsync(harness)
        );

        Assert.Equal("load sentinel", error.Message);
        Assert.Empty(harness.Fakes.Log.Of("GetEntryViewAsync"));
        Assert.Empty(harness.Fakes.Log.Of("SetActiveRoomAsync"));
        Assert.Empty(harness.Fakes.Log.Of("ClearActiveRoomAsync"));
        Assert.Empty(harness.Sent);
    }

    [Fact]
    public async Task ApprovedEntry_RechecksAccessAfterActivation()
    {
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        var checks = 0;
        harness.Fakes.Handlers["CheckEntryAccessAsync"] = _ =>
            Task.FromResult(
                ++checks == 1 ? RoomEntryAccessType.Allowed : RoomEntryAccessType.Banned
            );

        var packets = await OpenRoomAsync(harness);

        Assert.Equal(2, checks);
        Assert.Empty(harness.Fakes.Log.Of("GetEntryViewAsync"));
        Assert.Equal(
            [
                "OpenConnectionMessageComposer",
                "CantConnectMessageComposer",
                "CloseConnectionMessageComposer",
            ],
            packets
                .Select(packet => (int)packet.Header)
                .Select(header =>
                    new[]
                    {
                        "OpenConnectionMessageComposer",
                        "CantConnectMessageComposer",
                        "CloseConnectionMessageComposer",
                    }.Single(name => PacketHarness.Outgoing(name) == header)
                )
        );
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Doorbell_LoadsOnlyForAcceptedCurrentRequest(bool accepted, bool current)
    {
        var harness = CreateHarness(RoomEntryAccessType.Doorbell);
        harness.Fakes.Handlers["AnswerDoorbellAsync"] = _ => Task.FromResult<PlayerId?>(2);
        harness.Fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(
                new RoomPendingSnapshot
                {
                    RoomId = current ? 42 : 99,
                    State = RoomEntryState.RingingDoorbell,
                }
            );
        harness.Fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(true);
        harness.Fakes.Handlers["GetEntryViewAsync"] = _ =>
            Task.FromException<RoomEntryViewSnapshot>(
                new InvalidOperationException("view sentinel")
            );
        var service = (IRoomService)harness.Resolver.Resolve(typeof(IRoomService))!;
        var action = () =>
            service.AnswerDoorbellAsync(
                ActionContext.CreateForPlayer(1, 42),
                "visitor",
                accepted,
                CancellationToken.None
            );
        if (accepted && current)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(action);
            Assert.Equal("view sentinel", error.Message);
            Assert.Equal(
                new[] { "EnsureRoomActiveAsync", "SetPendingRoomAsync", "GetEntryViewAsync" },
                harness
                    .Fakes.Log.Calls.Where(c =>
                        c.Method
                            is "EnsureRoomActiveAsync"
                                or "SetPendingRoomAsync"
                                or "GetEntryViewAsync"
                    )
                    .Select(c => c.Method)
            );
        }
        else
        {
            await action();
            Assert.Empty(harness.Fakes.Log.Of("EnsureRoomActiveAsync"));
            Assert.Empty(harness.Fakes.Log.Of("GetEntryViewAsync"));
        }
    }

    [Fact]
    public async Task ApprovedEntry_SendsTheEventAndConfigurationStatesEvenWhenNone()
    {
        // Habbo, entering a room with no event and no configuration furni on: an empty
        // RoomEvent (ids -1) and ConfigurationItemStates all off, so nothing of the room before
        // is kept.
        var view = await new WiredRoom(8, 8).Harness.Room.GetEntryViewAsync(
            (PlayerId)1,
            CancellationToken.None
        );
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["GetEntryViewAsync"] = _ => Task.FromResult(view);

        var headers = (await OpenRoomAsync(harness)).Select(packet => (int)packet.Header).ToList();

        Assert.Contains(PacketHarness.Outgoing("RoomEventMessageComposer"), headers);
        Assert.Contains(PacketHarness.Outgoing("ConfigurationItemStatesMessageComposer"), headers);
    }

    [Fact]
    public void NoEvent_IsWrittenAsHabbosEmptyEvent()
    {
        var packet = PacketHarness.Encode(
            new Turbo.Primitives.Messages.Outgoing.Navigator.RoomEventMessageComposer
            {
                Event = null,
                SentAtUtc = DateTime.UtcNow,
            }
        );

        Assert.Equal(
            Convert.FromHexString(
                "FFFFFFFFFFFFFFFF0000FFFFFFFFFFFFFFFF000000000000000000000000FFFFFFFF"
            ),
            packet.PopBytes(packet.Remaining)
        );
    }

    [Fact]
    public async Task ApprovedEntry_OpensWithEmptyNftChatStylesRightBeforeRoomReady()
    {
        // hhus, 2026-10-13: OpenConnection, UserNftChatStyles (00000000 for an account without
        // NFT styles), RoomReady, ... on every entry.
        var harness = await ApprovedHarnessAsync();

        var packets = await OpenRoomAsync(harness);
        var headers = packets.Select(packet => (int)packet.Header).ToList();
        var styles = headers.IndexOf(PacketHarness.Outgoing("UserNftChatStylesMessageComposer"));

        Assert.True(styles >= 0, "UserNftChatStyles was not sent");
        Assert.Equal(PacketHarness.Outgoing("RoomReadyMessageComposer"), headers[styles + 1]);
        Assert.Equal(0, packets[styles].PopInt());
    }

    [Fact]
    public async Task EnteringAnEmptyRoom_SendsUsersButNoEmptyUserUpdate()
    {
        // hhus, 2026-10-13: an empty room's entry has Users (no one) but no UserUpdate until
        // there is someone to update.
        var harness = await ApprovedHarnessAsync();

        var headers = (await OpenRoomAsync(harness)).Select(packet => (int)packet.Header).ToList();

        Assert.Contains(PacketHarness.Outgoing("UsersMessageComposer"), headers);
        Assert.DoesNotContain(PacketHarness.Outgoing("UserUpdateMessageComposer"), headers);
    }

    [Fact]
    public async Task TheCameraStartsAtTheDoorTilesHeightWhenSomethingThatBlocksStackingStandsThere()
    {
        // Under Habbo's stacking-blocked bit 15 a blocked tile is negative: -32512 is "blocked,
        // standing at 1.0". Read with the old bit-14 mask and "negative means no tile" it was 0.
        var harness = await ApprovedHarnessAsync(map =>
            map.TileEncodedHeights[(map.DoorY * map.Width) + map.DoorX] = unchecked(
                (short)(0x8000 | 256)
            )
        );

        var floor = (await OpenRoomAsync(harness)).Single(packet =>
            packet.Header == PacketHarness.Outgoing("FloorHeightMapMessageComposer")
        );
        floor.PopBoolean();
        floor.PopInt();
        floor.PopString();
        Assert.Equal(0, floor.PopInt());
        floor.PopInt();
        floor.PopInt();

        Assert.Equal(1.0f, floor.PopFloat());
    }

    private static async Task<PacketHarness> ApprovedHarnessAsync(
        Action<Turbo.Primitives.Rooms.Snapshots.Mapping.RoomMapSnapshot>? editMap = null
    )
    {
        var view = await new WiredRoom(8, 8).Harness.Room.GetEntryViewAsync(
            (PlayerId)1,
            CancellationToken.None
        );
        editMap?.Invoke(view.Map);
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["GetEntryViewAsync"] = _ => Task.FromResult(view);
        return harness;
    }

    private static PacketHarness CreateHarness(RoomEntryAccessType access)
    {
        var harness = new PacketHarness();
        harness.Fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(new RoomPendingSnapshot { RoomId = -1, State = RoomEntryState.None });
        harness.Fakes.Handlers["GetPendingRoomEntryAsync"] = _ =>
            Task.FromResult(RoomEntrySnapshot.Default);
        harness.Fakes.Handlers["CheckEntryAccessAsync"] = _ => Task.FromResult(access);
        return harness;
    }

    private static Task<List<ClientPacket>> OpenRoomAsync(PacketHarness harness) =>
        harness.SendAsync(
            PacketHarness.Incoming("OpenFlatConnectionMessageEvent"),
            PacketHarness.Payload(writer => writer.Int(42).String("password").Int(-1))
        );
}
