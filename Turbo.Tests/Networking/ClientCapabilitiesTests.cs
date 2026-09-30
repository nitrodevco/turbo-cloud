using FluentAssertions;
using Turbo.Primitives.Networking.Capabilities;
using Xunit;

namespace Turbo.Tests.Networking;

public class ClientCapabilitiesTests
{
    [Fact]
    public void Negotiate_KeepsWhatBothSpeak_AtTheLowerVersion()
    {
        var accepted = ClientCapabilities.Negotiate([
            Cap(ClientCapabilities.PERMISSION_NODES, 7),
            Cap("voice.chat", 1),
        ]);

        accepted
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(Cap(ClientCapabilities.PERMISSION_NODES, 1));
    }

    [Fact]
    public void Negotiate_DropsVersionsBelowOne_AndDuplicates()
    {
        ClientCapabilities
            .Negotiate([Cap(ClientCapabilities.PERMISSION_NODES, 0)])
            .Should()
            .BeEmpty();
        ClientCapabilities
            .Negotiate([
                Cap(ClientCapabilities.PERMISSION_NODES, 1),
                Cap(ClientCapabilities.PERMISSION_NODES, 1),
            ])
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Negotiate_NothingAsked_AcceptsNothing()
    {
        ClientCapabilities.Negotiate([]).Should().BeEmpty();
    }

    private static ClientCapabilitySnapshot Cap(string name, int version) =>
        new() { Name = name, Version = version };
}
