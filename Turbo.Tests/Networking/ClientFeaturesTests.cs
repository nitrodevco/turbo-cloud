using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Frozen;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.Networking.Session;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Features;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Revisions.Revision20260909;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// Habbo's build 116 (FLASH34) ends its client hello with the protocol features it understands
/// (<c>acr.length, ...acr</c> in its hello composer), and reads newer layouts of some packets for
/// them. The session keeps the names the server knows and the encoder writes a packet in the
/// layout the session's client reads.
/// </summary>
public class ClientFeaturesTests
{
    private const string PRODUCTION = "WIN63-202609091217-117204808";

    private static readonly int HELLO = PacketHarness.Incoming("ClientHelloMessageEvent");

    private static ClientHelloMessage ParseHello(Action<PayloadWriter>? tail = null) =>
        (ClientHelloMessage)PacketHarness.Parse(HELLO, Hello(tail));

    private static byte[] Hello(Action<PayloadWriter>? tail = null) =>
        PacketHarness.Payload(w =>
        {
            w.String(PRODUCTION).String("FLASH34").Int(4).Int(2);
            tail?.Invoke(w);
        });

    [Fact]
    public void A_hello_that_ends_at_the_device_category_names_no_features()
    {
        var hello = ParseHello();

        hello.Production.Should().Be(PRODUCTION);
        hello.DeviceCategory.Should().Be(2);
        hello.Features.Should().BeEmpty();
    }

    [Fact]
    public void A_hello_with_the_tail_names_its_features_in_order()
    {
        var hello = ParseHello(w =>
            w.Int(5)
                .String("avatar_collectibles")
                .String("paid_name_change")
                .String("pet_inventory_locations")
                .String("private_marketplace_offers")
                .String("room_dimmer_lights")
        );

        hello
            .Features.Should()
            .Equal(
                ClientFeatures.AVATAR_COLLECTIBLES,
                ClientFeatures.PAID_NAME_CHANGE,
                ClientFeatures.PET_INVENTORY_LOCATIONS,
                ClientFeatures.PRIVATE_MARKETPLACE_OFFERS,
                ClientFeatures.ROOM_DIMMER_LIGHTS
            );
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_negative_count_names_no_features(int count)
    {
        ParseHello(w => w.Int(count).String("room_dimmer_lights")).Features.Should().BeEmpty();
    }

    /// <summary>
    /// A count far past what the packet holds is cut to what its bytes could hold before
    /// anything is allocated for it, and the names that are not there then fail the packet like
    /// any other short one.
    /// </summary>
    [Fact]
    public void A_count_the_packet_cannot_hold_is_refused_as_a_short_packet()
    {
        var parse = () => ParseHello(w => w.Int(int.MaxValue).String("room_dimmer_lights"));

        parse.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_long_list_is_cut_at_the_cap_and_an_overlong_name_is_dropped()
    {
        var hello = ParseHello(w =>
        {
            w.Int(1000).String(new string('x', ClientFeatures.MAX_NAME_LENGTH + 1));

            for (var i = 0; i < 200; i++)
                w.String($"feature_{i}");
        });

        // The overlong name took one of the places read.
        hello.Features.Should().HaveCount(ClientFeatures.MAX_ADVERTISED - 1);
        hello.Features.Should().OnlyContain(x => x.Length <= ClientFeatures.MAX_NAME_LENGTH);
    }

    [Fact]
    public async Task The_hello_leaves_the_session_with_only_the_names_the_server_knows()
    {
        var harness = new PacketHarness();
        var state = SessionState(harness.Fakes);

        await harness.SendAsync(
            HELLO,
            Hello(w =>
                w.Int(4)
                    .String("room_dimmer_lights")
                    .String("teleport_everywhere")
                    .String("ROOM_DIMMER_LIGHTS")
                    .String("paid_name_change")
            ),
            playerId: 0
        );

        state
            .ClientFeatures.Should()
            .BeEquivalentTo(ClientFeatures.ROOM_DIMMER_LIGHTS, ClientFeatures.PAID_NAME_CHANGE);
        state.RevisionId.Should().Be(PRODUCTION);
    }

    [Fact]
    public async Task A_hello_without_the_tail_leaves_the_session_with_no_features()
    {
        var harness = new PacketHarness();
        var state = SessionState(harness.Fakes);

        state.SetClientFeatures([ClientFeatures.ROOM_DIMMER_LIGHTS]);

        await harness.SendAsync(HELLO, Hello(), playerId: 0);

        state.ClientFeatures.Should().BeEmpty();
    }

    /// <summary>
    /// A room broadcast reaches every session as one composer instance, and its bytes are cached
    /// for all of them. Two sessions that read different layouts must each get their own, in
    /// whichever order they are written to.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void One_composer_is_written_in_each_sessions_layout_whoever_is_first(bool featureFirst)
    {
        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

        revisions.RegisterRevision(new Revision20260909());

        var encoder = Encoder(revisions);
        var newer = Session(revisions.DefaultRevisionId, ClientFeatures.ROOM_DIMMER_LIGHTS);
        var older = Session(revisions.DefaultRevisionId);
        var composer = Presets();
        var order = featureFirst ? new[] { newer, older } : [older, newer];

        var first = order.ToDictionary(x => x, x => Encode(encoder, x, composer));
        var second = order.ToDictionary(x => x, x => Encode(encoder, x, composer));

        // Three presets, one more boolean each.
        first[newer].Length.Should().Be(first[older].Length + 3);
        first[older].Should().Equal(Frame(PacketHarness.Revision.Serializers, composer));
        first[newer]
            .Should()
            .Equal(
                Frame(
                    PacketHarness.Revision.FeatureSerializers[ClientFeatures.ROOM_DIMMER_LIGHTS],
                    composer
                )
            );
        second[newer].Should().Equal(first[newer]);
        second[older].Should().Equal(first[older]);
    }

    [Fact]
    public void A_feature_the_revision_has_no_serializer_for_changes_nothing()
    {
        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

        revisions.RegisterRevision(new Revision20260909());

        var encoder = Encoder(revisions);
        var composer = Presets();

        Encode(
                encoder,
                Session(revisions.DefaultRevisionId, ClientFeatures.PAID_NAME_CHANGE),
                composer
            )
            .Should()
            .Equal(Encode(encoder, Session(revisions.DefaultRevisionId), composer));
    }

    [Fact]
    public void Of_two_features_claiming_one_composer_the_first_by_name_writes_it()
    {
        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

        revisions.RegisterRevision(
            new TwoClaimsRevision(
                new()
                {
                    [ClientFeatures.ROOM_DIMMER_LIGHTS] = new MarkSerializer(2),
                    [ClientFeatures.AVATAR_COLLECTIBLES] = new MarkSerializer(1),
                }
            )
        );

        var encoder = Encoder(revisions);

        int Mark(params string[] features) =>
            BinaryPrimitives.ReadInt32BigEndian(
                Encode(encoder, Session(TwoClaimsRevision.ID, features), new Marked()).AsSpan(6)
            );

        Mark().Should().Be(0);
        Mark(ClientFeatures.ROOM_DIMMER_LIGHTS).Should().Be(2);
        Mark(ClientFeatures.ROOM_DIMMER_LIGHTS, ClientFeatures.AVATAR_COLLECTIBLES).Should().Be(1);
    }

    // The session the handler is given, keeping its features as a real session does.
    private static SessionContextState SessionState(Fakes fakes)
    {
        var state = new SessionContextState(NullLogger<ISessionContext>.Instance);

        fakes.Handlers["SetRevisionId"] = call =>
        {
            state.RevisionId = (string)call.Args[0]!;

            return null;
        };
        fakes.Handlers["SetClientFeatures"] = call =>
        {
            state.SetClientFeatures((IEnumerable<string>)call.Args[0]!);

            return null;
        };

        return state;
    }

    private static ISessionContext Session(string revisionId, params string[] features)
    {
        var fakes = new Fakes();

        fakes.Handlers["get_RevisionId"] = _ => revisionId;
        fakes.Handlers["get_CryptoOut"] = _ => null;
        fakes.Handlers["get_ClientFeatures"] = _ => ClientFeatures.Known(features);

        return fakes.Create<ISessionContext>("session");
    }

    private static PackageEncoder Encoder(IRevisionManager revisions) =>
        new(
            revisions,
            new ExtensionPacketRegistry(),
            new ComposerPayloadCache(),
            new CapturingLogger<PackageEncoder>()
        );

    private static byte[] Encode(
        PackageEncoder encoder,
        ISessionContext session,
        IComposer composer
    )
    {
        var writer = new ArrayBufferWriter<byte>();

        encoder.Encode(writer, session, composer);

        return writer.WrittenSpan.ToArray();
    }

    private static byte[] Frame(
        IEnumerable<KeyValuePair<Type, ISerializer>> serializers,
        IComposer composer
    )
    {
        using var packet = serializers
            .Single(x => x.Key == composer.GetType())
            .Value.Serialize(composer);

        return packet.ToArray();
    }

    private static RoomDimmerPresetsMessageComposer Presets() =>
        new()
        {
            ItemId = 51,
            IsOn = true,
            SelectedPresetId = 2,
            Presets =
            [
                Preset(1, keepFurnitureLights: false),
                Preset(2, keepFurnitureLights: true),
                Preset(3, keepFurnitureLights: false),
            ],
        };

    private static DimmerPresetSnapshot Preset(int id, bool keepFurnitureLights) =>
        new()
        {
            Id = id,
            Type = 1,
            Color = "#0053F7",
            Brightness = 200,
            KeepFurnitureLights = keepFurnitureLights,
        };

    private sealed record Marked : IComposer;

    private sealed class MarkSerializer(int mark) : AbstractSerializer<Marked>(20011)
    {
        protected override void Serialize(IServerPacket packet, Marked message) =>
            packet.WriteInteger(mark);
    }

    private sealed class TwoClaimsRevision(Dictionary<string, ISerializer> claims) : IRevision
    {
        public const string ID = "two-claims";

        public string Revision => ID;

        public IDictionary<int, IParser> Parsers { get; } = new Dictionary<int, IParser>();

        public IDictionary<Type, ISerializer> Serializers { get; } =
            new Dictionary<Type, ISerializer> { [typeof(Marked)] = new MarkSerializer(0) };

        public IReadOnlyDictionary<
            string,
            IReadOnlyDictionary<Type, ISerializer>
        > FeatureSerializers { get; } =
            claims.ToFrozenDictionary(
                x => x.Key,
                x =>
                    (IReadOnlyDictionary<Type, ISerializer>)
                        new Dictionary<Type, ISerializer> { [typeof(Marked)] = x.Value }
            );
    }
}
