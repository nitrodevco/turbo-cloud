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
