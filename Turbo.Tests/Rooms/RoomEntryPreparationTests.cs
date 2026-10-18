using Turbo.Primitives.Action;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
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
            // Habbo answers a full room with CantConnect alone; the connection closes on the
            // client's Quit (official client capture 2026-10-18, 3 of 3 entries).
            RoomEntryAccessType.Full =>
            [
                "OpenConnectionMessageComposer",
                "CantConnectMessageComposer",
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
    public async Task Doorbell_TellsTheRingerToWaitWithANamelessDoorbell()
    {
        // Official client capture 2026-10-13 (ringing a doorbell room): OpenConnection, ...,
        // Doorbell "" - the client's "waiting for an answer" view (navigator/IncomingMessages.as
        // onDoorbell shows it only for an empty name). Turbo told the ringer nothing.
        var harness = CreateHarness(RoomEntryAccessType.Doorbell);
        harness.Fakes.Handlers["RingDoorbellAsync"] = _ => Task.FromResult(true);
        harness.Fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(
                new PlayerSummarySnapshot
                {
                    PlayerId = 1,
                    Name = "visitor",
                    Motto = string.Empty,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    AchievementScore = 0,
                    BadgesRank = 0,
                    IsOnline = true,
                    CreatedAt = DateTime.UnixEpoch,
                    LastUpdated = DateTime.UnixEpoch,
                    RespectPoints = 0,
                    RespectsLeft = 0,
                    PetRespectsLeft = 0,
                    RespectReplenishesLeft = 0,
                }
            );

        var packets = await OpenRoomAsync(harness);

        Assert.Equal(
            new[] { "OpenConnectionMessageComposer", "DoorbellMessageComposer" }.Select(
                PacketHarness.Outgoing
            ),
            packets.Select(packet => (int)packet.Header)
        );
        Assert.Equal(string.Empty, packets[1].PopString());
    }

    [Fact]
    public async Task Doorbell_AnAcceptedRingerIsToldTheDoorOpenedBeforeTheRoomLoads()
    {
        // Official client capture 2026-10-13 (let in after ringing): FlatAccessible (room, "")
        // then RoomReady; the empty name closes the waiting view (IncomingMessages.as onDoorOpened).
        var view = await new WiredRoom(8, 8).Harness.Room.GetEntryViewAsync(
            (PlayerId)2,
            CancellationToken.None
        );
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["GetEntryViewAsync"] = _ => Task.FromResult(view);
        harness.Fakes.Handlers["AnswerDoorbellAsync"] = _ => Task.FromResult<PlayerId?>(2);
        harness.Fakes.Handlers["GetPendingRoomAsync"] = _ =>
            Task.FromResult(
                new RoomPendingSnapshot { RoomId = 42, State = RoomEntryState.RingingDoorbell }
            );
        harness.Fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(true);
        var service = (IRoomService)harness.Resolver.Resolve(typeof(IRoomService))!;

        await service.AnswerDoorbellAsync(
            ActionContext.CreateForPlayer(1, 42),
            "visitor",
            true,
            CancellationToken.None
        );

        var packets = harness.Sent.Select(PacketHarness.Encode).ToList();
        var headers = packets.Select(packet => (int)packet.Header).ToList();
        var opened = headers.IndexOf(PacketHarness.Outgoing("FlatAccessibleMessageComposer"));

        Assert.True(opened >= 0, "FlatAccessible was not sent");
        Assert.True(opened < headers.IndexOf(PacketHarness.Outgoing("RoomReadyMessageComposer")));
        Assert.Equal(42, packets[opened].PopInt());
        Assert.Equal(string.Empty, packets[opened].PopString());
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
    public async Task ApprovedEntry_AsksForTheRoomsPollOfferAfterTheEntry()
    {
        // The client takes a poll offer only for the room session it has (AS3
        // PollHandler.onPollOfferEvent), so the player's polls are asked once the room is theirs.
        var harness = await ApprovedHarnessAsync();

        await OpenRoomAsync(harness);

        var calls = harness.Fakes.Log.Calls.Select(x => x.Method).ToList();

        Assert.Contains("OfferAsync", calls);
        Assert.True(calls.LastIndexOf("SetActiveRoomAsync") < calls.IndexOf("OfferAsync"));
    }

    [Fact]
    public async Task ApprovedEntry_TellsThePresenceTheOwnerTheRoomHolds()
    {
        // The presence counts a visit to another player's room; the owner comes from the room
        // that admitted the player, not from a database read on every entry.
        var harness = await ApprovedHarnessAsync();

        await OpenRoomAsync(harness);

        var view = await (Task<RoomEntryViewSnapshot>)
            harness.Fakes.Handlers["GetEntryViewAsync"](null!)!;
        var call = Assert.Single(harness.Fakes.Log.Of("SetActiveRoomAsync"));
        Assert.Equal(view.Room.OwnerId, Assert.IsType<PlayerId>(call.Args[1]));
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
    public async Task ApprovedEntry_NamesThePurchasableChatStylesThePlayerOwns()
    {
        // The selector offers a purchasable bubble (10000 to 99999, custom.chat.styles.enabled)
        // only to an owner UserPurchasableChatStyles names (RoomChatInputView.isPurchasableStyle,
        // SessionDataManager.onPurchasableChatStyles); Turbo never sent it.
        var harness = await ApprovedHarnessAsync();
        harness.Fakes.Handlers["GetOwnedPurchasableChatStyleIdsAsync"] = _ =>
            Task.FromResult(new List<int> { 10001, 10020 });

        var packets = await OpenRoomAsync(harness);
        var styles = packets.Single(packet =>
            packet.Header == PacketHarness.Outgoing("UserPurchasableChatStylesMessageComposer")
        );

        Assert.Equal(2, styles.PopInt());
        Assert.Equal(10001, styles.PopInt());
        Assert.Equal(10020, styles.PopInt());
    }

    [Fact]
    public async Task ApprovedEntry_SendsNoPurchasableChatStylesToAPlayerWithout()
    {
        // 325 official client captures of accounts without any: never sent.
        var harness = await ApprovedHarnessAsync();
        harness.Fakes.Handlers["GetOwnedPurchasableChatStyleIdsAsync"] = _ =>
            Task.FromResult(new List<int>());

        var packets = await OpenRoomAsync(harness);

        Assert.DoesNotContain(
            packets,
            packet =>
                packet.Header == PacketHarness.Outgoing("UserPurchasableChatStylesMessageComposer")
        );
    }

    [Fact]
    public async Task ApprovedEntry_NamesTheNftChatStylesThePlayerOwns()
    {
        // The client offers an NFT bubble (1000 to 9999) only to an owner UserNftChatStyles
        // names (RoomChatInputView.as:374-379); Turbo always sent an empty list.
        var harness = await ApprovedHarnessAsync();
        harness.Fakes.Handlers["GetOwnedNftChatStyleIdsAsync"] = _ =>
            Task.FromResult(new List<int> { 1001, 1050 });

        var packets = await OpenRoomAsync(harness);
        var styles = packets.Single(packet =>
            packet.Header == PacketHarness.Outgoing("UserNftChatStylesMessageComposer")
        );

        Assert.Equal(2, styles.PopInt());
        Assert.Equal(1001, styles.PopInt());
        Assert.Equal(1050, styles.PopInt());
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

    [Fact]
    public async Task ApprovedEntry_SendsTheRoomInHabbosOrder()
    {
        // hhus, four entries on two builds (2026-10-13): RoomReady, RoomEntryTile, RoomProperty x3,
        // HeightMap, FloorHeightMap, RoomVisualizationSettings, Users, Objects, Items. The rest,
        // from the player's own avatar to RaidProtectionCapability, comes from the room with the
        // avatar (RoomEntryTailTests). RoomChatSettings is not part of an entry (the client
        // takes the room's chat settings from GetGuestRoomResult; Habbo sends it on a change).
        var view = await new WiredRoom(8, 8).Harness.Room.GetEntryViewAsync(
            (PlayerId)1,
            CancellationToken.None
        );
        view = view with
        {
            Properties =
            [
                new(RoomPropertyType.Floor, "110"),
                new(RoomPropertyType.Wall, "201"),
                new(RoomPropertyType.Landscape, "1.1"),
            ],
        };
        var harness = CreateHarness(RoomEntryAccessType.Allowed);
        harness.Fakes.Handlers["GetEntryViewAsync"] = _ => Task.FromResult(view);

        var headers = (await OpenRoomAsync(harness)).Select(packet => (int)packet.Header).ToList();
        string[] order =
        [
            "RoomReadyMessageComposer",
            "RoomEntryTileMessageComposer",
            "RoomPropertyMessageComposer",
            "HeightMapMessageComposer",
            "FloorHeightMapMessageComposer",
            "RoomVisualizationSettingsMessageComposer",
            "UsersMessageComposer",
            "ObjectsMessageComposer",
            "ItemsMessageComposer",
        ];
        var firsts = order.Select(name => headers.IndexOf(PacketHarness.Outgoing(name))).ToList();

        Assert.All(
            firsts,
            index => Assert.True(index >= 0, "missing: " + order[firsts.IndexOf(index)])
        );
        Assert.Equal(firsts.Order().ToList(), firsts);
        Assert.Equal(
            3,
            headers.Count(h => h == PacketHarness.Outgoing("RoomPropertyMessageComposer"))
        );
        Assert.DoesNotContain(PacketHarness.Outgoing("RoomChatSettingsMessageComposer"), headers);
        Assert.Equal(PacketHarness.Outgoing("ItemsMessageComposer"), headers[^1]);
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
