using FluentAssertions;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// Wired chests, wired trades and transaction logs in the bytes the 20260909 client reads and
/// writes: each is checked against the field order of the AS3 client's parser or composer
/// (<c>userdefinedroomevents/wiredtrading</c>, <c>ChestItemType</c>, <c>ChestStorage</c>).
/// </summary>
public class WiredTradingProtocolTests
{
    private static readonly ChestItemTypeSnapshot FloorType = new()
    {
        IsWallItem = false,
        TypeId = 230,
        LegacyPosterId = "",
    };

    private static readonly ChestItemTypeSnapshot PosterType = new()
    {
        IsWallItem = true,
        TypeId = 4001,
        LegacyPosterId = "52",
    };

    [Fact]
    public void WithdrawItems_ReadsTheChest_TheFurniType_ThenTheAmount()
    {
        var header = PacketHarness.Incoming("WithdrawItemsFromChestMessageEvent");
        var payload = PacketHarness.Payload(w =>
            w.Int(77).Bool(true).Int(4001).String("52").Int(3)
        );

        var message = PacketHarness.Parse(header, payload);

        message
            .Should()
            .BeEquivalentTo(
                new WithdrawItemsFromChestMessage
                {
                    ChestId = 77,
                    ItemType = PosterType,
                    Amount = 3,
                }
            );
    }

    [Fact]
    public void AChestFurniType_IsValueEqual_SoItCanKeyACount()
    {
        var counts = new Dictionary<ChestItemTypeSnapshot, int> { [PosterType] = 2 };

        counts
            .Should()
            .ContainKey(
                new ChestItemTypeSnapshot
                {
                    IsWallItem = true,
                    TypeId = 4001,
                    LegacyPosterId = "52",
                }
            );
    }

    [Fact]
    public void AChestContentsChunk_WritesTheExtraInt_ForAFloorItemOnly()
    {
        var packet = PacketHarness.Encode(
            new ItemsChestContentsChunkMessageComposer
            {
                ChestId = 5,
                TotalFragments = 1,
                FragmentNo = 0,
                Items = [Stored(10, PosterType, extra: 99), Stored(11, FloorType, extra: 42)],
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("ItemsChestContentsChunkMessageComposer"));
        packet.PopInt().Should().Be(5);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(2);

        ReadStoredHead(packet, 10, PosterType);
        // A wall item ends with its stuff data: the next int is the floor item's id, not 99.
        ReadStoredHead(packet, 11, FloorType);
        packet.PopInt().Should().Be(42);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void ATradeOfRules_WritesTheRuleSet_AndTheMultiplierItsTypeReads()
    {
        var packet = PacketHarness.Encode(
            new WiredTradeInitiateMessageComposer
            {
                Requirement = new TradeRequirementSnapshot
                {
                    Type = TradeRequirementType.Rules,
                    YouGetText = "A throne",
                    LayoutType = "default",
                    Rules = new TradeRequirementRulesSnapshot
                    {
                        Definition = new TradeRequirementRulesDefinitionSnapshot
                        {
                            YouGive =
                            [
                                Rule(
                                    new TradeRequirementNodeSnapshot
                                    {
                                        Type = TradeRequirementNodeType.Coin,
                                        Amount = 50,
                                        ItemType = null,
                                    }
                                ),
                            ],
                            YouGet = Rule(
                                new TradeRequirementNodeSnapshot
                                {
                                    Type = TradeRequirementNodeType.Furni,
                                    Amount = 1,
                                    ItemType = FloorType,
                                }
                            ),
                        },
                        Type = TradeRequirementRulesType.Multiplier,
                        Multiplier = 3,
                        AutoMultiplierMax = 8,
                    },
                },
                ShowRequirementsImmediate = true,
                OverridePreviousTrade = false,
                TimeoutSeconds = 60,
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredTradeInitiateMessageComposer"));
        packet.PopInt().Should().Be(4);
        packet.PopString().Should().Be("A throne");
        packet.PopString().Should().Be("default");

        // You give: one rule of one coin node, which has no furni type after it.
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(1);
        packet.PopByte().Should().Be(0);
        packet.PopInt().Should().Be(50);

        // You get: one furni node, followed by its type.
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(1);
        packet.PopByte().Should().Be(1);
        packet.PopInt().Should().Be(1);
        ReadType(packet, FloorType);

        // Multiplier type, then only the multiplier (not the auto-multiplier cap).
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(3);

        packet.PopBoolean().Should().BeTrue();
        packet.PopBoolean().Should().BeFalse();
        packet.PopInt().Should().Be(60);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void ARewardSuccess_CarriesTheReward_TextAndOpenFlag()
    {
        var packet = PacketHarness.Encode(
            new WiredTransactionSuccessMessageComposer
            {
                Contents = new WiredTransactionSuccessContentsSnapshot
                {
                    Type = WiredTransactionSuccessType.Rewarded,
                    RewardContents = Rule(
                        new TradeRequirementNodeSnapshot
                        {
                            Type = TradeRequirementNodeType.Coin,
                            Amount = 25,
                            ItemType = null,
                        }
                    ),
                    RewardText = "Well done",
                    OpenByDefault = true,
                },
            }
        );

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredTransactionSuccessMessageComposer"));
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(1);
        packet.PopByte().Should().Be(0);
        packet.PopInt().Should().Be(25);
        packet.PopString().Should().Be("Well done");
        packet.PopBoolean().Should().BeTrue();
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void ADepositSuccess_IsOnlyItsType()
    {
        var packet = PacketHarness.Encode(
            new WiredTransactionSuccessMessageComposer
            {
                Contents = new WiredTransactionSuccessContentsSnapshot
                {
                    Type = WiredTransactionSuccessType.Deposit,
                    RewardContents = Rule(
                        new TradeRequirementNodeSnapshot
                        {
                            Type = TradeRequirementNodeType.Coin,
                            Amount = 25,
                            ItemType = null,
                        }
                    ),
                    RewardText = "ignored",
                    OpenByDefault = true,
                },
            }
        );

        packet.PopInt().Should().Be(0);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public void LogDetails_WriteTheRow_TheChests_ThenTheDepositedAndWithdrawnCounts()
    {
        var packet = PacketHarness.Encode(
            new WiredTransactionLogDetailsMessageComposer
            {
                Details = new WiredTransactionDetailsSnapshot
                {
                    Info = new WiredTransactionInfoSnapshot
                    {
                        TransactionId = 9_000_000_001L,
                        FlatId = 12,
                        Type = WiredTransactionType.Wired,
                        DefinitionInfo = "def",
                        UserId = 3,
                        UserName = "alice",
                        Timestamp = 1_700_000_000_000L,
                        ReadableTimestamp = "today",
                        ChestCount = 2,
                        WithdrawFurniCount = 1,
                        DepositFurniCount = 4,
                        WithdrawCoinsCount = 0,
                        DepositCoinsCount = 10,
                    },
                    ChestIds = [100, 101],
                    Deposited =
                    [
                        new WiredTransactionItemCountSnapshot { Type = FloorType, Count = 4 },
                    ],
                    Withdrawn =
                    [
                        new WiredTransactionItemCountSnapshot { Type = PosterType, Count = 1 },
                    ],
                    IsIncompleteData = true,
                },
            }
        );

        packet
            .Header.Should()
            .Be(PacketHarness.Outgoing("WiredTransactionLogDetailsMessageComposer"));
        packet.PopLong().Should().Be(9_000_000_001L);
        packet.PopInt().Should().Be(12);
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("def");
        packet.PopInt().Should().Be(3);
        packet.PopString().Should().Be("alice");
        packet.PopLong().Should().Be(1_700_000_000_000L);
        packet.PopString().Should().Be("today");
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(4);
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(10);

        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(100);
        packet.PopInt().Should().Be(101);

        packet.PopInt().Should().Be(1);
        ReadType(packet, FloorType);
        packet.PopInt().Should().Be(4);

        packet.PopInt().Should().Be(1);
        ReadType(packet, PosterType);
        packet.PopInt().Should().Be(1);

        packet.PopBoolean().Should().BeTrue();
        packet.End.Should().BeTrue();
    }

    private static TradeRequirementRuleSnapshot Rule(params TradeRequirementNodeSnapshot[] nodes) =>
        new() { Nodes = [.. nodes] };

    private static ChestStorageSnapshot Stored(int id, ChestItemTypeSnapshot type, int extra) =>
        new()
        {
            ItemId = id,
            LockState = 0,
            TransactionId = 1234L,
            Type = type,
            Groupable = true,
            SpecialType = 1,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "on" },
            Extra = extra,
        };

    private static void ReadStoredHead(IClientPacket packet, int id, ChestItemTypeSnapshot type)
    {
        packet.PopInt().Should().Be(id);
        packet.PopInt().Should().Be(0);
        packet.PopLong().Should().Be(1234L);
        ReadType(packet, type);
        packet.PopBoolean().Should().BeTrue();
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(0);
        packet.PopString().Should().Be("on");
    }

    private static void ReadType(IClientPacket packet, ChestItemTypeSnapshot type)
    {
        packet.PopBoolean().Should().Be(type.IsWallItem);
        packet.PopInt().Should().Be(type.TypeId);
        packet.PopString().Should().Be(type.LegacyPosterId);
    }
}
