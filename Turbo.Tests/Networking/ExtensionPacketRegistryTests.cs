using FluentAssertions;
using Turbo.Networking.Extensions;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Packets;
using Xunit;

namespace Turbo.Tests.Networking;

public class ExtensionPacketRegistryTests
{
    private readonly ExtensionPacketRegistry _registry = new();

    [Theory]
    [InlineData(0)]
    [InlineData(4101)] // Habbo's own space
    [InlineData(ExtensionPackets.LAST_RESERVED_CORE_HEADER)]
    [InlineData(ExtensionPackets.FIRST_TURBO_HEADER)]
    [InlineData(ExtensionPackets.LAST_TURBO_HEADER)]
    [InlineData(32768)] // does not fit the signed 16-bit wire header
    [InlineData(-1)]
    public void RegisterParser_OutsideThePluginRange_IsRefused(int header)
    {
        var act = () => _registry.RegisterParser(header, new TestParser());

        act.Should().Throw<ArgumentOutOfRangeException>();
        _registry.TryGetParser(header, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(ExtensionPackets.FIRST_PLUGIN_HEADER)]
    [InlineData(ExtensionPackets.FIRST_TURBO_HEADER - 1)]
    [InlineData(ExtensionPackets.LAST_TURBO_HEADER + 1)]
    [InlineData(ExtensionPackets.LAST_PLUGIN_HEADER)]
    public void RegisterParser_AtTheEdgesOfThePluginSpace_Works(int header)
    {
        using var reg = _registry.RegisterParser(header, new TestParser());

        _registry.TryGetParser(header, out _).Should().BeTrue();
    }

    [Fact]
    public void RegisterSerializer_OutsideThePluginRange_IsRefused()
    {
        var act = () => _registry.RegisterSerializer(typeof(TestComposer), new TestSerializer(100));

        act.Should().Throw<ArgumentOutOfRangeException>();
        _registry.TryGetSerializer(typeof(TestComposer), out _).Should().BeFalse();
    }

    [Fact]
    public void RegisterParser_ForATakenHeader_IsRefused_AndTheFirstStays()
    {
        var first = new TestParser();
        using var reg = _registry.RegisterParser(20010, first);

        var act = () => _registry.RegisterParser(20010, new TestParser());

        act.Should().Throw<InvalidOperationException>();
        _registry.TryGetParser(20010, out var kept).Should().BeTrue();
        kept.Should().BeSameAs(first);
    }

    [Fact]
    public void RegisterSerializer_ForATakenType_IsRefused()
    {
        using var reg = _registry.RegisterSerializer(
            typeof(TestComposer),
            new TestSerializer(20010)
        );

        var act = () =>
            _registry.RegisterSerializer(typeof(TestComposer), new TestSerializer(20011));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Disposing_RemovesTheRegistration_AndFreesIt()
    {
        var parser = _registry.RegisterParser(20010, new TestParser());
        var serializer = _registry.RegisterSerializer(
            typeof(TestComposer),
            new TestSerializer(20010)
        );
        var capability = _registry.RegisterCapability("test.extension");

        parser.Dispose();
        serializer.Dispose();
        capability.Dispose();

        _registry.TryGetParser(20010, out _).Should().BeFalse();
        _registry.TryGetSerializer(typeof(TestComposer), out _).Should().BeFalse();
        _registry.HasCapability("test.extension").Should().BeFalse();
        using var again = _registry.RegisterParser(20010, new TestParser());
    }

    [Fact]
    public void ADisposedHandle_DoesNotRemoveALaterRegistration()
    {
        var stale = _registry.RegisterParser(20010, new TestParser());
        stale.Dispose();
        using var current = _registry.RegisterParser(20010, new TestParser());

        stale.Dispose();

        _registry.TryGetParser(20010, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(ClientCapabilities.PERMISSION_NODES)]
    public void RegisterCapability_WithAnEmptyOrCoreName_IsRefused(string name)
    {
        var act = () => _registry.RegisterCapability(name);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterCapability_ForATakenName_IsRefused()
    {
        using var reg = _registry.RegisterCapability("test.extension");

        var act = () => _registry.RegisterCapability("test.extension");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Negotiate_AcceptsARegisteredName_AndRejectsUnknownOnes()
    {
        using var reg = _registry.RegisterCapability("test.extension");

        var accepted = ClientCapabilities.Negotiate(
            [
                Cap("test.extension", 9),
                Cap("not.registered", 1),
                Cap(ClientCapabilities.PERMISSION_NODES, 5),
            ],
            _registry
        );

        accepted
            .Should()
            .Equal(Cap(ClientCapabilities.PERMISSION_NODES, 1), Cap("test.extension", 1));
    }

    [Fact]
    public void Negotiate_AfterTheCapabilityIsDisposed_RejectsIt()
    {
        _registry.RegisterCapability("test.extension").Dispose();

        ClientCapabilities.Negotiate([Cap("test.extension", 1)], _registry).Should().BeEmpty();
    }

    private static ClientCapabilitySnapshot Cap(string name, int version) =>
        new() { Name = name, Version = version };

    private sealed record TestComposer : IComposer;

    private sealed record TestMessage : IMessageEvent;

    private sealed class TestParser : IParser
    {
        public IMessageEvent Parse(IClientPacket packet) => new TestMessage();
    }

    private sealed class TestSerializer(int header) : ISerializer
    {
        public int Header => header;

        public IServerPacket Serialize(IComposer message) => throw new NotSupportedException();
    }
}
