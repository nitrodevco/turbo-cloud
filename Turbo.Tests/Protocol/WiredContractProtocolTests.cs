using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// Wired contracts in the bytes the 20260909 client reads and writes: checked against the AS3
/// contents parser (<c>_-42y._-02m</c>), the update result parser (<c>_-42y._-b2i</c>) and
/// <c>AbstractContract.addContentsToComposer</c> with its payment and reward overrides.
/// </summary>
public class WiredContractProtocolTests
{
    private static readonly ChestItemTypeSnapshot PosterType = new()
    {
        IsWallItem = true,
        TypeId = 4001,
        LegacyPosterId = "52",
    };

    [Fact]
    public void APaymentUpdate_ReadsEveryGiveRule_ThenThePaymentTail()
    {
        var payload = PacketHarness.Payload(w =>
            w.Int(31)
                .Short(0)
                // You give: two alternatives, a poster or 25 coins.
                .Bool(true)
                .Int(2)
                .Int(1)
                .Byte(1)
                .Int(2)
                .Bool(true)
                .Int(4001)
                .String("52")
                .Int(1)
                .Byte(0)
                .Int(25)
                // You get: nothing.
                .Bool(false)
                .Short(1)
                .String("Thanks!")
                .String("games")
        );

        var message = PacketHarness.Parse(
            PacketHarness.Incoming("WiredUpdateContractMessageEvent"),
            payload
        );

        message
            .Should()
            .BeEquivalentTo(
                new WiredUpdateContractMessage
                {
                    Contract = new WiredContractSnapshot
                    {
                        ContractId = 31,
                        Type = WiredContractType.Payment,
                        Definition = new TradeRequirementRulesDefinitionSnapshot
                        {
                            YouGive = [Rule(Furni(2, PosterType)), Rule(Coins(25))],
                            YouGet = null,
                        },
                        PaymentMode = WiredContractPaymentMode.Specific,
                        ReceiveText = "Thanks!",
                        LayoutType = "games",
                    },
                }
            );
    }

    [Fact]
    public void ARewardUpdate_ReadsTheGetRule_ThenTheRewardTail()
    {
        var payload = PacketHarness.Payload(w =>
            w.Int(8)
                .Short(2)
                .Bool(false)
                .Bool(true)
                .Int(1)
                .Byte(0)
                .Int(100)
                .Short(13)
                .Bool(true)
                .String("Well played")
        );

        var message = PacketHarness.Parse(
            PacketHarness.Incoming("WiredUpdateContractMessageEvent"),
            payload
        );

        message
            .Should()
            .BeEquivalentTo(
                new WiredUpdateContractMessage
                {
                    Contract = new WiredContractSnapshot
                    {
                        ContractId = 8,
                        Type = WiredContractType.Reward,
                        Definition = new TradeRequirementRulesDefinitionSnapshot
                        {
                            YouGive = null,
                            YouGet = Rule(Coins(100)),
                        },
                        RewardCategory = (int)WiredEarningsCategory.Agency,
                        ShowDialog = true,
                        RewardText = "Well played",
                    },
                }
            );
    }

    [Fact]
    public void AHugeRuleCount_IsCappedAtWhatThePacketHolds()
    {
        // The count claims int.MaxValue rules; the packet holds two empty ones and the get flag.
        var payload = PacketHarness.Payload(w =>
            w.Int(4).Short(1).Bool(true).Int(int.MaxValue).Int(0).Int(0).Bool(false)
        );

        var message = (WiredUpdateContractMessage)
            PacketHarness.Parse(PacketHarness.Incoming("WiredUpdateContractMessageEvent"), payload);

        message.Contract.Type.Should().Be(WiredContractType.Trade);
        message.Contract.Definition.YouGive.Should().NotBeNull();
        message.Contract.Definition.YouGive!.Value.Should().HaveCount(2);
        message.Contract.Definition.YouGive!.Value.Should().OnlyContain(r => r.Nodes.IsEmpty);
        message.Contract.Definition.YouGet.Should().BeNull();
    }

    [Fact]
    public void TradeContents_EndAtTheDefinition_WithoutAPaymentOrRewardTail()
    {
        var packet = PacketHarness.Encode(
            new WiredContractContentsMessageComposer
            {
                Contract = new WiredContractSnapshot
                {
                    ContractId = 12,
                    Type = WiredContractType.Trade,
                    Definition = new TradeRequirementRulesDefinitionSnapshot
                    {
                        YouGive = [Rule(Coins(10))],
                        YouGet = Rule(Furni(1, PosterType)),
                    },
                    // Set but not for a trade: none of these may reach the wire.
                    PaymentMode = WiredContractPaymentMode.Specific,
                    ReceiveText = "ignored",
                    LayoutType = "generic",
                    RewardCategory = 11,
                    ShowDialog = true,
                    RewardText = "ignored",
                },
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredContractContentsMessageComposer"));
        packet.PopInt().Should().Be(12);
        packet.PopShort().Should().Be(1);

        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(1);
        packet.PopByte().Should().Be(0);
        packet.PopInt().Should().Be(10);

        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(1);
        packet.PopByte().Should().Be(1);
        packet.PopInt().Should().Be(1);
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(4001);
        packet.PopString().Should().Be("52");

        packet.End.Should().BeTrue();
    }

    [Fact]
    public void PaymentContents_WriteThePaymentTail_AsShorts_AndStrings()
    {
        var packet = PacketHarness.Encode(
            new WiredContractContentsMessageComposer
            {
                Contract = new WiredContractSnapshot
                {
                    ContractId = 3,
                    Type = WiredContractType.Payment,
                    Definition = new TradeRequirementRulesDefinitionSnapshot
                    {
                        YouGive = [],
                        YouGet = null,
                    },
                    PaymentMode = WiredContractPaymentMode.Donation,
                    ReceiveText = "Tip jar",
                    LayoutType = "generic",
                },
            }
        );

        packet.PopInt().Should().Be(3);
        packet.PopShort().Should().Be(0);
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(0);
        packet.PopBoolean().Should().BeFalse();
        packet.PopShort().Should().Be(0);
        packet.PopString().Should().Be("Tip jar");
        packet.PopString().Should().Be("generic");
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void OpenAndResult_CarryTheContractId_AndTheFailCode()
    {
        PacketHarness
            .Parse(
                PacketHarness.Incoming("WiredOpenContractMessageEvent"),
                PacketHarness.Payload(w => w.Int(55))
            )
            .Should()
            .BeEquivalentTo(new WiredOpenContractMessage { ContractId = 55 });

        var open = PacketHarness.Encode(new WiredOpenContractMessageComposer { ContractId = 55 });
        open.Header.Should().Be(PacketHarness.Outgoing("WiredOpenContractMessageComposer"));
        open.PopInt().Should().Be(55);
        open.End.Should().BeTrue();

        var result = PacketHarness.Encode(
            new WiredContractUpdateResultMessageComposer
            {
                ContractId = 55,
                IsSuccess = false,
                FailCode = WiredContractFailCodes.INVALID_RULES,
            }
        );
        result
            .Header.Should()
            .Be(PacketHarness.Outgoing("WiredContractUpdateResultMessageComposer"));
        result.PopInt().Should().Be(55);
        result.PopBoolean().Should().BeFalse();
        result.PopString().Should().Be("invalid_rules");
        result.End.Should().BeTrue();
    }

    private static TradeRequirementRuleSnapshot Rule(params TradeRequirementNodeSnapshot[] nodes) =>
        new() { Nodes = [.. nodes] };

    private static TradeRequirementNodeSnapshot Coins(int amount) =>
        new()
        {
            Type = TradeRequirementNodeType.Coin,
            Amount = amount,
            ItemType = null,
        };

    private static TradeRequirementNodeSnapshot Furni(int amount, ChestItemTypeSnapshot type) =>
        new()
        {
            Type = TradeRequirementNodeType.Furni,
            Amount = amount,
            ItemType = type,
        };
}
