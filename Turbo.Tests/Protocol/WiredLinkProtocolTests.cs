using System.Collections.Immutable;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired.Link;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// WIRED Link window requests and answers in wire bytes, as the official hotel sends them
/// (official client capture 2026-10-21, build WIN63-202610201032, own Links in two own rooms), in
/// the layouts of the AS3 parsers (<c>wiredlink/_-X29</c> result, <c>_-e1E</c> configuration,
/// <c>_-7s</c> connections, <c>WiredLinkConfigurationInvalidatedMessageParser</c>).
/// </summary>
public class WiredLinkProtocolTests
{
    private const int ROOM = 80462556;
    private const int LINK = 2147418112;
    private const int OTHER_LINK = 2147418113;

    public static TheoryData<string, int, byte[]> Requests() =>
        new()
        {
            { "WiredLinkGetConfigurationMessageEvent", 0, PacketHarness.Payload(w => w.Int(LINK)) },
            {
                "WiredLinkSaveConfigurationMessageEvent",
                1,
                PacketHarness.Payload(w => w.Int(LINK).String("0").Bool(false))
            },
            {
                "WiredLinkCreateNetworkMessageEvent",
                2,
                PacketHarness.Payload(w => w.Int(LINK).String("lab"))
            },
            {
                "WiredLinkRenameNetworkMessageEvent",
                3,
                PacketHarness.Payload(w => w.Int(LINK).String("5").String("lab"))
            },
            {
                "WiredLinkDeleteNetworkMessageEvent",
                4,
                PacketHarness.Payload(w => w.Int(LINK).String("5"))
            },
            {
                "WiredLinkGetConnectionsMessageEvent",
                5,
                PacketHarness.Payload(w => w.Int(LINK).String("5"))
            },
        };

    /// <summary>Recorded: a save naming a table instead of a Link answers code 7 and nothing else.</summary>
    [Theory]
    [MemberData(nameof(Requests))]
    public async Task A_request_for_a_furni_that_is_no_link_of_the_room_answers_code_7(
        string request,
        int operation,
        byte[] payload
    )
    {
        var harness = Harness(WiredLinkResultCodes.LINK_UNAVAILABLE);

        var reply = Assert.Single(
            await harness.SendAsync(PacketHarness.Incoming(request), payload, roomId: ROOM)
        );

        AssertRefused(reply, operation, WiredLinkResultCodes.LINK_UNAVAILABLE);
    }

    [Fact]
    public async Task Someone_elses_link_answers_code_1()
    {
        var harness = Harness(WiredLinkResultCodes.NOT_OWNER);

        var reply = Assert.Single(
            await harness.SendAsync(
                PacketHarness.Incoming("WiredLinkCreateNetworkMessageEvent"),
                PacketHarness.Payload(w => w.Int(LINK).String("lab")),
                roomId: ROOM
            )
        );

        AssertRefused(reply, 2, WiredLinkResultCodes.NOT_OWNER);
    }

    [Fact]
    public async Task The_window_opens_with_the_links_configuration_and_every_network()
    {
        var harness = Harness(WiredLinkResultCodes.OK);
        harness.Fakes.Handlers[nameof(IWiredLinkOwnerGrain.GetConfigurationAsync)] = _ =>
            Task.FromResult(
                new WiredLinkConfigurationSnapshot
                {
                    SelectedNetworkId = "10",
                    ReceiveLocal = true,
                    Networks =
                    [
                        Network("10", "LabMax 1", 1, true),
                        Network("8", "LabMax 10", 0, false),
                    ],
                }
            );

        var reply = Assert.Single(
            await harness.SendAsync(
                PacketHarness.Incoming("WiredLinkGetConfigurationMessageEvent"),
                PacketHarness.Payload(w => w.Int(LINK)),
                roomId: ROOM
            )
        );

        Assert.Equal(PacketHarness.Outgoing("WiredLinkConfigurationMessageComposer"), reply.Header);
        Assert.Equal(ROOM, reply.PopInt());
        Assert.Equal(LINK, reply.PopInt());
        Assert.Equal("10", reply.PopString());
        Assert.True(reply.PopBoolean());
        Assert.Equal(2, reply.PopInt());
        AssertNetwork(reply, "10", "LabMax 1", 1, true);
        AssertNetwork(reply, "8", "LabMax 10", 0, false);
        Assert.True(reply.End);
    }

    [Fact]
    public async Task A_save_answers_the_configuration_and_the_networks_it_changed()
    {
        var harness = Harness(WiredLinkResultCodes.OK);
        harness.Fakes.Handlers[nameof(IWiredLinkOwnerGrain.SaveAsync)] = _ =>
            Task.FromResult(
                new WiredLinkOperationOutcome
                {
                    ResultCode = WiredLinkResultCodes.OK,
                    SelectedNetworkId = "12",
                    ReceiveLocal = false,
                    Networks = [Network("10", "old", 0, false), Network("12", "new", 1, true)],
                }
            );

        var reply = Assert.Single(
            await harness.SendAsync(
                PacketHarness.Incoming("WiredLinkSaveConfigurationMessageEvent"),
                PacketHarness.Payload(w => w.Int(LINK).String("12").Bool(false)),
                roomId: ROOM
            )
        );

        Assert.Equal(
            PacketHarness.Outgoing("WiredLinkOperationResultMessageComposer"),
            reply.Header
        );
        Assert.Equal(ROOM, reply.PopInt());
        Assert.Equal(LINK, reply.PopInt());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.Equal("12", reply.PopString());
        Assert.False(reply.PopBoolean());
        Assert.Equal("0", reply.PopString());
        Assert.Equal(2, reply.PopInt());
        AssertNetwork(reply, "10", "old", 0, false);
        AssertNetwork(reply, "12", "new", 1, true);
        Assert.True(reply.End);
    }

    [Fact]
    public async Task A_delete_tells_the_other_links_of_the_room_their_network_is_gone()
    {
        var harness = Harness(WiredLinkResultCodes.OK);
        harness.Fakes.Handlers[nameof(IWiredLinkOwnerGrain.DeleteNetworkAsync)] = _ =>
            Task.FromResult(
                new WiredLinkOperationOutcome
                {
                    ResultCode = WiredLinkResultCodes.OK,
                    SelectedNetworkId = "0",
                    ReceiveLocal = true,
                    RemovedNetworkId = "12",
                    Disconnected = [new(ROOM, OTHER_LINK), new(80463631, 5)],
                }
            );

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("WiredLinkDeleteNetworkMessageEvent"),
            PacketHarness.Payload(w => w.Int(LINK).String("12")),
            roomId: ROOM
        );

        Assert.Equal(2, replies.Count);

        var result = replies[0];

        Assert.Equal(
            PacketHarness.Outgoing("WiredLinkOperationResultMessageComposer"),
            result.Header
        );
        Assert.Equal(ROOM, result.PopInt());
        Assert.Equal(LINK, result.PopInt());
        Assert.Equal(4, result.PopInt());
        Assert.Equal(0, result.PopInt());
        Assert.True(result.PopBoolean());
        Assert.Equal("0", result.PopString());
        Assert.True(result.PopBoolean());
        Assert.Equal("12", result.PopString());
        Assert.Equal(0, result.PopInt());

        var invalidated = replies[1];

        Assert.Equal(
            PacketHarness.Outgoing("WiredLinkConfigurationInvalidatedMessageComposer"),
            invalidated.Header
        );
        Assert.Equal(ROOM, invalidated.PopInt());
        Assert.Equal(OTHER_LINK, invalidated.PopInt());
        Assert.Equal("12", invalidated.PopString());
        Assert.True(invalidated.End);
    }

    [Fact]
    public async Task Connections_list_each_room_with_its_link()
    {
        var harness = Harness(WiredLinkResultCodes.OK);
        harness.Fakes.Handlers[nameof(IWiredLinkOwnerGrain.GetConnectionsAsync)] = _ =>
            Task.FromResult<ImmutableArray<WiredLinkConnectionSnapshot>?>([
                new WiredLinkConnectionSnapshot
                {
                    RoomId = 80462556,
                    RoomName = "Hutch.'s  room",
                    FurniId = LINK,
                    IsBuildersClub = true,
                    ReceiveLocal = false,
                },
            ]);

        var reply = Assert.Single(
            await harness.SendAsync(
                PacketHarness.Incoming("WiredLinkGetConnectionsMessageEvent"),
                PacketHarness.Payload(w => w.Int(LINK).String("8")),
                roomId: ROOM
            )
        );

        Assert.Equal(PacketHarness.Outgoing("WiredLinkConnectionsMessageComposer"), reply.Header);
        Assert.Equal(ROOM, reply.PopInt());
        Assert.Equal(LINK, reply.PopInt());
        Assert.Equal("8", reply.PopString());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(80462556, reply.PopInt());
        Assert.Equal("Hutch.'s  room", reply.PopString());
        Assert.Equal(LINK, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.False(reply.PopBoolean());
        Assert.True(reply.End);
    }

    [Fact]
    public async Task Connections_of_an_unknown_network_answer_code_2()
    {
        var harness = Harness(WiredLinkResultCodes.OK);
        harness.Fakes.Handlers[nameof(IWiredLinkOwnerGrain.GetConnectionsAsync)] = _ =>
            Task.FromResult<ImmutableArray<WiredLinkConnectionSnapshot>?>(null);

        var reply = Assert.Single(
            await harness.SendAsync(
                PacketHarness.Incoming("WiredLinkGetConnectionsMessageEvent"),
                PacketHarness.Payload(w => w.Int(LINK).String("999999")),
                roomId: ROOM
            )
        );

        AssertRefused(reply, 5, WiredLinkResultCodes.NETWORK_NOT_FOUND);
    }

    [Fact]
    public async Task Outside_a_room_a_link_request_gets_no_answer()
    {
        var replies = await new PacketHarness().SendAsync(
            PacketHarness.Incoming("WiredLinkGetConfigurationMessageEvent"),
            PacketHarness.Payload(w => w.Int(LINK))
        );

        Assert.Empty(replies);
    }

    /// <summary>
    /// A Link Speaker's bubble (recorded Chat h3184): objectId -1, style 253, no links, tracking
    /// -1, the viewer as receiver, no width, the name hidden, then the speaker's tile.
    /// </summary>
    [Fact]
    public void A_speaker_bubble_ends_with_its_tile()
    {
        var packet = PacketHarness.Encode(
            new ChatMessageComposer
            {
                ObjectId = -1,
                Text = "lab hello",
                Gesture = AvatarGestureType.None,
                StyleId = 253,
                Links = [],
                TrackingId = -1,
                ReceiverRoomIndex = 0,
                HideUsername = true,
                Location = new ChatBubbleLocation(3, 7),
            }
        );

        Assert.Equal(-1, packet.PopInt());
        Assert.Equal("lab hello", packet.PopString());
        Assert.Equal(0, packet.PopInt());
        Assert.Equal(253, packet.PopInt());
        Assert.Equal(0, packet.PopInt());
        Assert.Equal(-1, packet.PopInt());
        Assert.Equal(0, packet.PopInt());
        Assert.Equal(-1, packet.PopInt());
        Assert.True(packet.PopBoolean());
        Assert.Equal(3, packet.PopInt());
        Assert.Equal(7, packet.PopInt());
        Assert.True(packet.End);
    }

    private static PacketHarness Harness(int check)
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers[nameof(IRoomGrain.CheckWiredLinkAsync)] = _ =>
            Task.FromResult((check, true));

        return harness;
    }

    private static WiredLinkNetworkSnapshot Network(
        string id,
        string name,
        int count,
        bool linked
    ) =>
        new()
        {
            NetworkId = id,
            Name = name,
            ConnectionCount = count,
            LinkedInCurrentRoom = linked,
        };

    private static void AssertRefused(ClientPacket reply, int operation, int code)
    {
        Assert.Equal(
            PacketHarness.Outgoing("WiredLinkOperationResultMessageComposer"),
            reply.Header
        );
        Assert.Equal(ROOM, reply.PopInt());
        Assert.Equal(LINK, reply.PopInt());
        Assert.Equal(operation, reply.PopInt());
        Assert.Equal(code, reply.PopInt());
        Assert.False(reply.PopBoolean());
        Assert.Equal("0", reply.PopString());
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.End);
    }

    private static void AssertNetwork(
        ClientPacket reply,
        string id,
        string name,
        int count,
        bool linked
    )
    {
        Assert.Equal(id, reply.PopString());
        Assert.Equal(name, reply.PopString());
        Assert.Equal(count, reply.PopInt());
        Assert.Equal(linked, reply.PopBoolean());
    }
}
