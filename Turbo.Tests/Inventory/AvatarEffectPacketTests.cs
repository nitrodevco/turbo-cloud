using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;
using Turbo.Primitives.Messages.Outgoing.Inventory.Avatareffect;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// The effect messages as the client reads and writes them: field order and types, taken from
/// the client's own parsers (<c>AvatarEffectActivatedMessageParser</c> and the others), and
/// that a request reaches the effect grain with the id the client named.
/// </summary>
public sealed class AvatarEffectPacketTests
{
    private const int ACTIVATE_HEADER = 2036;
    private const int SELECT_HEADER = 2995;

    [Fact]
    public void TheClientsHeaderIdsAreTheOnesTheRevisionKnows()
    {
        PacketHarness.Incoming("AvatarEffectActivatedMessageEvent").Should().Be(ACTIVATE_HEADER);
        PacketHarness.Incoming("AvatarEffectSelectedMessageEvent").Should().Be(SELECT_HEADER);
    }

    [Fact]
    public void ActivateReadsTheEffectType()
    {
        var message = PacketHarness.Parse(ACTIVATE_HEADER, PacketHarness.Payload(w => w.Int(108)));

        message.Should().BeOfType<AvatarEffectActivatedMessage>().Which.Type.Should().Be(108);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-1)]
    public void SelectReadsTheEffectTypeIncludingTheMinusOneTheOfficialClientSendsToTakeOneOff(
        int type
    )
    {
        var message = PacketHarness.Parse(SELECT_HEADER, PacketHarness.Payload(w => w.Int(type)));

        message.Should().BeOfType<AvatarEffectSelectedMessage>().Which.Type.Should().Be(type);
    }

    [Fact]
    public void AddedWritesTypeSubTypeDurationAndPermanent()
    {
        var packet = PacketHarness.Encode(
            new AvatarEffectAddedMessageComposer
            {
                Type = 7,
                SubType = 1,
                Duration = 3600,
                IsPermanent = false,
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("AvatarEffectAddedMessageComposer"));
        packet.PopInt().Should().Be(7);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(3600);
        packet.PopBoolean().Should().BeFalse();
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void ActivatedWritesTypeDurationAndPermanent()
    {
        var packet = PacketHarness.Encode(
            new AvatarEffectActivatedMessageComposer
            {
                Type = 7,
                Duration = 600,
                IsPermanent = true,
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("AvatarEffectActivatedMessageComposer"));
        packet.PopInt().Should().Be(7);
        packet.PopInt().Should().Be(600);
        packet.PopBoolean().Should().BeTrue();
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void ExpiredAndSelectedWriteTheTypeAlone()
    {
        var expired = PacketHarness.Encode(new AvatarEffectExpiredMessageComposer { Type = 12 });
        var selected = PacketHarness.Encode(new AvatarEffectSelectedMessageComposer { Type = 12 });

        expired.Header.Should().Be(PacketHarness.Outgoing("AvatarEffectExpiredMessageComposer"));
        expired.PopInt().Should().Be(12);
        expired.Remaining.Should().Be(0);

        selected.Header.Should().Be(PacketHarness.Outgoing("AvatarEffectSelectedMessageComposer"));
        selected.PopInt().Should().Be(12);
        selected.Remaining.Should().Be(0);
    }

    [Fact]
    public void TheListWritesTheCountThenSixFieldsPerEffectWithMinusOneForNotRunning()
    {
        var packet = PacketHarness.Encode(
            new AvatarEffectsMessageComposer
            {
                Effects =
                [
                    new AvatarEffectSnapshot
                    {
                        Type = 7,
                        SubType = 0,
                        Duration = 600,
                        InactiveEffectsInInventory = 2,
                        SecondsLeftIfActive = -1,
                        IsPermanent = false,
                    },
                ],
            }
        );

        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(7);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(600);
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(-1);
        packet.PopBoolean().Should().BeFalse();
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task ActivateReachesTheEffectGrainWithTheIdTheClientNamed()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            ACTIVATE_HEADER,
            PacketHarness.Payload(w => w.Int(42)),
            playerId: 5
        );

        var call = harness.Fakes.Log.Of(nameof(IPlayerEffectGrain.ActivateEffectAsync)).Single();
        call.Args[0].Should().Be(42);
        call.Key.Should().Be(5L);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task SelectReachesTheEffectGrainWithWhateverTheClientSent(int type)
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            SELECT_HEADER,
            PacketHarness.Payload(w => w.Int(type)),
            playerId: 5
        );

        harness
            .Fakes.Log.Of(nameof(IPlayerEffectGrain.SelectEffectAsync))
            .Single()
            .Args[0]
            .Should()
            .Be(type);
    }

    [Fact]
    public async Task ARequestFromNoPlayerReachesNoGrain()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            ACTIVATE_HEADER,
            PacketHarness.Payload(w => w.Int(42)),
            playerId: 0
        );
        await harness.SendAsync(SELECT_HEADER, PacketHarness.Payload(w => w.Int(42)), playerId: 0);

        harness.Fakes.Log.On<IPlayerEffectGrain>().Should().BeEmpty();
    }
}
