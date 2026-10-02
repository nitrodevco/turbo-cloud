using FluentAssertions;
using Turbo.Primitives.Commands.Enums;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The <c>chat.commands</c> packets in the bytes a client reads and writes, in the order
/// <c>docs/client-capabilities.md</c> gives them.
/// </summary>
public class CommandTreeProtocolTests
{
    [Fact]
    public void TheTree_IsEachCommandThenEachOfItsParameters()
    {
        var packet = PacketHarness.Encode(
            new TurboCommandTreeMessage
            {
                Tree = new CommandTreeSnapshot
                {
                    Commands =
                    [
                        new CommandTreeEntrySnapshot
                        {
                            Name = "give",
                            Aliases = ["dar"],
                            Category = "Support",
                            Description = "Add to a balance",
                            Usage = ":give <who> <currency>",
                            RoomLevel = -1,
                            Operator = true,
                            Parameters =
                            [
                                new CommandTreeParameterSnapshot
                                {
                                    Name = "who",
                                    Kind = CommandParameterKind.Player,
                                    Optional = false,
                                    Suggest = CommandSuggestType.Server,
                                    Members = [],
                                    Selectors = true,
                                },
                                new CommandTreeParameterSnapshot
                                {
                                    Name = "size",
                                    Kind = CommandParameterKind.Enumeration,
                                    Optional = true,
                                    Suggest = CommandSuggestType.Client,
                                    Members = ["small", "large"],
                                    Selectors = false,
                                },
                            ],
                        },
                    ],
                },
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("TurboCommandTreeMessageComposer"));
        packet.Header.Should().Be(30002);
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("give");
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("dar");
        packet.PopString().Should().Be("Support");
        packet.PopString().Should().Be("Add to a balance");
        packet.PopString().Should().Be(":give <who> <currency>");
        packet.PopInt().Should().Be(-1);
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(2);

        packet.PopString().Should().Be("who");
        packet.PopInt().Should().Be(6);
        packet.PopBoolean().Should().BeFalse();
        packet.PopInt().Should().Be(2);
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(0);
        ReadMetadata();

        packet.PopString().Should().Be("size");
        packet.PopInt().Should().Be(4);
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(1);
        packet.PopBoolean().Should().BeFalse();
        packet.PopInt().Should().Be(2);
        packet.PopString().Should().Be("small");
        packet.PopString().Should().Be("large");
        ReadMetadata();
        packet.PopInt().Should().Be(0); // syntax branches
        packet.End.Should().BeTrue();

        void ReadMetadata()
        {
            packet.PopString().Should().BeEmpty();
            packet.PopString().Should().BeEmpty();
            packet.PopString().Should().BeEmpty();
            packet.PopInt().Should().Be(-1);
            packet.PopInt().Should().Be(-1);
            packet.PopString().Should().BeEmpty();
        }
    }

    [Fact]
    public void TheSuggestions_AreTheRequestIdThenTheValues()
    {
        var packet = PacketHarness.Encode(
            new TurboCommandSuggestionsMessage { RequestId = 7, Values = ["Alice", "Albert"] }
        );

        packet.Header.Should().Be(30004);
        packet.PopInt().Should().Be(7);
        packet.PopInt().Should().Be(2);
        packet.PopString().Should().Be("Alice");
        packet.PopString().Should().Be("Albert");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void TheRequest_IsReadInItsOrder_WithHostileLengthsCut()
    {
        var message = (TurboCommandSuggestMessage)
            PacketHarness.Parse(
                30003,
                PacketHarness.Payload(w =>
                    w.Int(9)
                        .String("ban")
                        .Int(0)
                        .String(new string('a', 500))
                        .String("check")
                        .String("Alice")
                )
            );

        message.Syntax.Should().Be("check");
        message.ArgumentText.Should().Be("Alice");
        message.RequestId.Should().Be(9);
        message.Command.Should().Be("ban");
        message.Parameter.Should().Be(0);
        message.Prefix.Should().HaveLength(ClientCapabilities.MAX_SUGGEST_TEXT);
    }

    [Fact]
    public void TheExtension_IsOneTheServerSpeaks()
    {
        ClientCapabilities
            .Negotiate([
                new ClientCapabilitySnapshot
                {
                    Name = ClientCapabilities.CHAT_COMMANDS,
                    Version = 3,
                },
            ])
            .Should()
            .ContainSingle()
            .Which.Version.Should()
            .Be(1);
    }
}
