using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans.Serialization;
using Orleans.Streams;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Revisions.Revision20260909;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's composer sent to a room (docs/plugin-packets.md): the room's stream serializes what
/// it carries, and the host's serializer knows no plugin's types, so the room writes the composer
/// with the plugin's serializer first. Each session in the room is sent the same bytes the plugin's
/// serializer writes, and only one that accepted its capability.
/// </summary>
public sealed class PluginComposerRoomSendTests
{
    private const string CAPABILITY = "test.hud";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_plugin_composer_sent_to_the_room_crosses_its_stream_as_the_plugin_wrote_it()
    {
        var registry = new ExtensionPacketRegistry();
        using var registration = registry.RegisterSerializer(
            typeof(HudState),
            new HudStateSerializer()
        );
        var room = RoomWith(registry);

        await room.Room.SendComposerToRoomAsync(new HudState(7), Ct);
        await room.Room.SendComposersToRoomAsync([new HudState(8), new HudState(9)], Ct);

        var published = room.Fakes.RoomPublishes().ToList();
        published.Should().HaveCount(2);

        // What the stream does with every item.
        var decoded = published.Select(RoundTrip).SelectMany(x => x.Composers).ToList();

        decoded.Should().AllBeOfType<WrittenExtensionPacket>();
        decoded
            .Cast<WrittenExtensionPacket>()
            .Should()
            .AllSatisfy(x => x.Capability.Should().Be(CAPABILITY));

        var encoded = decoded.Select(x => Encode(registry, x)).ToList();
        encoded
            .Select(x => BinaryPrimitives.ReadInt32BigEndian(x.AsSpan(6, 4)))
            .Should()
            .Equal(7, 8, 9);
        encoded[0].Should().Equal(Encode(registry, new HudState(7)), "the plugin's own framing");
    }

    [Fact]
    public async Task A_core_composer_sent_to_the_room_is_left_as_it_is()
    {
        var room = RoomWith(new ExtensionPacketRegistry());
        var composer = new Turbo.Primitives.Messages.Outgoing.Turbo.TurboServerCapabilitiesMessage
        {
            Capabilities = [],
        };

        await room.Room.SendComposerToRoomAsync(composer, Ct);

        room.Fakes.RoomPublishes().Single().Composers.Should().Equal(composer);
    }

    [Fact]
    public async Task A_written_plugin_packet_reaches_only_sessions_that_accepted_its_capability() =>
        await SessionHarness.InTurns(async () =>
        {
            var hotel = new SessionHarness();
            var session = hotel.NewSession(1);
            var key = session.SessionKey;

            await hotel.Gateway.AddSessionAsync(key, session);
            await hotel.Gateway.AddSessionToPlayerAsync(key, SessionHarness.PlayerId);

            var forHud = Written(CAPABILITY);
            var forEveryone = Written(string.Empty);

            await hotel.PresenceGrain.TrySendComposerAsync(forHud, Ct);
            await hotel.PresenceGrain.TrySendComposerAsync(forEveryone, Ct);
            await hotel.WhenPresenceIdleAsync();

            hotel.Received(key).Should().Equal([forEveryone], "the client never asked for the hud");

            await hotel.PresenceGrain.SetClientCapabilitiesAsync(
                [new ClientCapabilitySnapshot { Name = CAPABILITY, Version = 1 }],
                Ct
            );
            await hotel.PresenceGrain.TrySendComposerAsync(forHud, Ct);
            await hotel.WhenPresenceIdleAsync();

            hotel.Received(key).Should().Equal(forEveryone, forHud);
        });

    private static WrittenExtensionPacket Written(string capability) =>
        new() { Capability = capability, Bytes = [0, 0, 0, 2, 0x4E, 0x2A] };

    private static RoomHarness RoomWith(IExtensionPacketRegistry registry)
    {
        var room = new RoomHarness();

        room.Room._roomOutbound = room.Fakes.Create<IAsyncStream<RoomOutboundSnapshot>>("stream");
        typeof(RoomGrain)
            .GetField("_extensions", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(room.Room, registry);

        return room;
    }

    private static RoomOutboundSnapshot RoundTrip(RoomOutboundSnapshot snapshot)
    {
        var services = new ServiceCollection();
        services.AddSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<Serializer>();
        var decoded = serializer.Deserialize<RoomOutboundSnapshot>(
            serializer.SerializeToArray(snapshot)
        );

        Assert.NotNull(decoded);

        return decoded;
    }

    /// <summary>The bytes a session is written for the composer.</summary>
    private static byte[] Encode(IExtensionPacketRegistry registry, IComposer composer)
    {
        var fakes = new Fakes();
        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

        revisions.RegisterRevision(new Revision20260909());
        fakes.Handlers["get_RevisionId"] = _ => revisions.DefaultRevisionId;
        fakes.Handlers["get_CryptoOut"] = _ => null;

        var writer = new ArrayBufferWriter<byte>();

        new PackageEncoder(
            revisions,
            registry,
            new ComposerPayloadCache(),
            NullLogger<PackageEncoder>.Instance
        ).Encode(writer, fakes.Create<ISessionContext>("session"), composer);

        return writer.WrittenSpan.ToArray();
    }

    private sealed record HudState(int Value) : ICapabilityComposer
    {
        public string Capability => CAPABILITY;
    }

    private sealed class HudStateSerializer() : AbstractSerializer<HudState>(20006)
    {
        protected override void Serialize(IServerPacket packet, HudState message) =>
            packet.WriteInteger(message.Value);
    }
}
