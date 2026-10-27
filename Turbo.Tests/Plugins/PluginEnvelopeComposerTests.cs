using System.Buffers;
using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Messages.PluginMessages;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Revisions.Revision20260909;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// An envelope (<see cref="PluginEnvelopeComposer"/>): a plugin message whose fields are followed
/// by a whole packet of the hotel's, its short header and body exactly as the session's encoder
/// writes it alone, for a client module to hand to <c>api.packets.parseCore</c>. The carried
/// packet is written once for every session it goes to.
/// </summary>
public sealed class PluginEnvelopeComposerTests : IDisposable
{
    private readonly Fakes _fakes = new();
    private readonly ExtensionPacketRegistry _extensions = new();
    private readonly PluginMessageDirectory _directory;
    private readonly IDisposable _registration;
    private readonly PackageEncoder _encoder;

    public PluginEnvelopeComposerTests()
    {
        _directory = new PluginMessageDirectory(_extensions);
        _registration = _directory.Register("test", [typeof(Portal), typeof(Ping)]);

        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

        revisions.RegisterRevision(new Revision20260909());
        _fakes.Handlers["get_RevisionId"] = _ => revisions.DefaultRevisionId;
        _fakes.Handlers["get_CryptoOut"] = _ => null;
        _encoder = new PackageEncoder(
            revisions,
            _extensions,
            new ComposerPayloadCache(),
            NullLogger<PackageEncoder>.Instance,
            _directory
        );
    }

    public void Dispose() => _registration.Dispose();

    [Fact]
    public void An_envelope_is_the_plugin_messages_header_and_fields_then_the_core_packet_as_written_alone()
    {
        var inner = new TurboServerCapabilitiesMessage { Capabilities = [] };
        var alone = Encode(inner);
        _directory.TryGetHeader("test.portal", out var header);

        var bytes = Encode(PluginEnvelopeComposer.For(new Portal(42), inner));

        BinaryPrimitives.ReadInt32BigEndian(bytes).Should().Be(bytes.Length - 4);
        BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(4)).Should().Be((short)header);
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(6)).Should().Be(42, "the plugin's field");
        bytes[10..].Should().Equal(alone[4..], "the core packet's header and body, no length");
    }

    [Fact]
    public void A_plugins_written_packet_and_a_plugin_message_ride_in_an_envelope_the_same_way()
    {
        var written = new WrittenExtensionPacket
        {
            Capability = string.Empty,
            Bytes = [0, 0, 0, 6, 0x4E, 0x2A, 0, 0, 0, 9],
        };
        var message = PluginMessageComposer.For(new Ping(5));

        Encode(PluginEnvelopeComposer.For(new Portal(1), written))[10..]
            .Should()
            .Equal(0x4E, 0x2A, 0, 0, 0, 9);
        Encode(PluginEnvelopeComposer.For(new Portal(1), message))[10..]
            .Should()
            .Equal(Encode(message)[4..]);
    }

    [Fact]
    public void The_carried_packet_is_written_once_for_every_session_and_envelope()
    {
        var serializer = new CountingSerializer();
        using var registration = _extensions.RegisterSerializer(typeof(Counted), serializer);
        var inner = new Counted(7);

        var forRoom3 = Encode(PluginEnvelopeComposer.For(new Portal(3), inner));
        var forRoom4 = Encode(PluginEnvelopeComposer.For(new Portal(4), inner));
        Encode(inner);

        serializer.Calls.Should().Be(1);
        forRoom3[10..].Should().Equal(forRoom4[10..]);
    }

    [Fact]
    public void An_envelope_whose_plugin_has_gone_or_that_carries_an_envelope_is_not_written()
    {
        var inner = new TurboServerCapabilitiesMessage { Capabilities = [] };
        var envelope = PluginEnvelopeComposer.For(new Portal(1), inner);

        Encode(envelope with { Inner = envelope }).Should().BeEmpty();
        FluentActions
            .Invoking(() => PluginEnvelopeComposer.For(new Portal(1), envelope))
            .Should()
            .Throw<ArgumentException>();

        _registration.Dispose();
        Encode(envelope).Should().BeEmpty();
    }

    private byte[] Encode(IComposer composer)
    {
        var writer = new ArrayBufferWriter<byte>();

        _encoder.Encode(writer, _fakes.Create<ISessionContext>("session"), composer);

        return writer.WrittenSpan.ToArray();
    }

    [PluginMessage("test.portal")]
    public sealed record Portal(int RoomId) : IPluginMessage;

    [PluginMessage("test.ping")]
    public sealed record Ping(int Value) : IPluginMessage;

    private sealed record Counted(int Value) : IComposer;

    private sealed class CountingSerializer() : AbstractSerializer<Counted>(20007)
    {
        public int Calls { get; private set; }

        protected override void Serialize(IServerPacket packet, Counted message)
        {
            Calls++;
            packet.WriteInteger(message.Value);
        }
    }
}
