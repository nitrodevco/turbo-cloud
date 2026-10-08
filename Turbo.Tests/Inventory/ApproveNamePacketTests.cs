using FluentAssertions;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Pets.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// <c>ApproveName</c> as the client writes and reads it: the name then its kind (1, a pet) from
/// the catalog's pet pages, and the result then the info text back, per the client's
/// <c>ApproveNameMessageParser</c>.
/// </summary>
public sealed class ApproveNamePacketTests
{
    private const int PET = 1;

    [Fact]
    public async Task APetName_ReachesTheBuyersInventory()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("ApproveNameMessageEvent"),
            PacketHarness.Payload(w => w.String("Rex").Int(PET)),
            playerId: 5
        );

        var call = harness
            .Fakes.Log.Of(nameof(IInventoryGrain.SendPetNameApprovalAsync))
            .Should()
            .ContainSingle()
            .Subject;

        call.Args[0].Should().Be("Rex");
        call.Key.Should().Be(5L);
    }

    [Fact]
    public async Task AnUnknownKind_IsNotApproved()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("ApproveNameMessageEvent"),
            PacketHarness.Payload(w => w.String("Rex").Int(9)),
            playerId: 5
        );

        harness.Fakes.Log.Of(nameof(IInventoryGrain.SendPetNameApprovalAsync)).Should().BeEmpty();
    }

    [Fact]
    public void TheResultWritesTheStatusThenTheInfo()
    {
        var packet = PacketHarness.Encode(
            new ApproveNameMessageComposer
            {
                Result = PetNameValidationType.TooLong,
                NameValidationInfo = "15",
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("ApproveNameMessageComposer"));
        packet.PopInt().Should().Be((int)PetNameValidationType.TooLong);
        packet.PopString().Should().Be("15");
        packet.Remaining.Should().Be(0);
    }
}
