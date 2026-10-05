using System.Buffers;
using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Messages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

public class ExtensionPacketHotPathTests
{
    private readonly Fakes _fakes = new();
    private readonly RevisionManager _revisions = new(NullLogger<RevisionManager>.Instance);
    private readonly ExtensionPacketRegistry _registry;
    private readonly ISessionContext _session;
    private readonly CapturingLogger<PackageEncoder> _encoderLog = new();

    public ExtensionPacketHotPathTests()
    {
        _revisions.RegisterRevision(new Revision20260909());
        _registry = new ExtensionPacketRegistry();
        _fakes.Handlers["get_RevisionId"] = _ => _revisions.DefaultRevisionId;
        _fakes.Handlers["get_CryptoOut"] = _ => null;
        _session = _fakes.Create<ISessionContext>("session");
    }

    [Fact]
    public void Encode_AComposerNobodyWrites_StillWritesNothing()
    {
        Encode(new Composer(7)).Should().BeEmpty();
    }

    [Fact]
    public void Encode_ARegisteredPluginComposer_IsFramedWithItsHeader()
    {
        using var reg = _registry.RegisterSerializer(typeof(Composer), new Serializer());

        var bytes = Encode(new Composer(7));

        BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(4, 2)).Should().Be(20010);
        BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(6, 4)).Should().Be(7);
    }

    [Fact]
    public void Encode_AfterTheRegistrationIsDisposed_WritesNothingAgain()
    {
        _registry.RegisterSerializer(typeof(Composer), new Serializer()).Dispose();

        Encode(new Composer(7)).Should().BeEmpty();
    }

    [Fact]
    public void Encode_ACoreComposer_IsUnchangedByTheOverlay()
    {
        using var reg = _registry.RegisterCapability("test.extension");

        var bytes = Encode(new TurboServerCapabilitiesMessage { Capabilities = [] });

        BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(4, 2)).Should().Be(30000);
    }

    [Fact]
    public async Task Handle_ARegisteredPluginHeader_IsParsed_NotLoggedAsUnknown()
    {
        var parser = new CountingParser();
        using var reg = _registry.RegisterParser(20010, parser);
        var logger = new CapturingLogger<PackageHandler>();

        await Handler(logger)
            .Handle(
                (IAppSession)_session,
                new ClientPacket(20010, new byte[] { 0, 0, 0, 5 }),
                CancellationToken.None
            );

        parser.Parsed.Should().Be(1);
        logger.Entries.Should().NotContain(x => x.Message.Contains("Unknown"));
    }

    [Fact]
    public async Task Handle_AnUnregisteredHeader_IsLoggedAsUnknown_AsBefore()
    {
        var logger = new CapturingLogger<PackageHandler>();

        await Handler(logger)
            .Handle(
                (IAppSession)_session,
                new ClientPacket(20010, new byte[] { 0, 0, 0, 5 }),
                CancellationToken.None
            );

        logger
            .Entries.Should()
            .Contain(x => x.Level == LogLevel.Warning && x.Message.Contains("Unknown"));
    }

    private PackageHandler Handler(ILogger<PackageHandler> logger) =>
        new(
            _revisions,
            _registry,
            new MessageSystem(
                new MessageRegistry(
                    new ServiceCollection().BuildServiceProvider(),
                    NullLogger<MessageRegistry>.Instance
                )
            ),
            logger
        );

    private byte[] Encode(IComposer composer)
    {
        var encoder = new PackageEncoder(
            _revisions,
            _registry,
            new ComposerPayloadCache(),
            _encoderLog
        );
        var writer = new ArrayBufferWriter<byte>();

        encoder.Encode(writer, _session, composer);

        return writer.WrittenSpan.ToArray();
    }

    private sealed record Composer(int Value) : IComposer;

    private sealed record Message(int Value) : IMessageEvent;

    private sealed class Serializer() : AbstractSerializer<Composer>(20010)
    {
        protected override void Serialize(IServerPacket packet, Composer message) =>
            packet.WriteInteger(message.Value);
    }

    private sealed class CountingParser : IParser
    {
        public int Parsed { get; private set; }

        public IMessageEvent Parse(IClientPacket packet)
        {
            Parsed++;

            return new Message(packet.PopInt());
        }
    }
}
