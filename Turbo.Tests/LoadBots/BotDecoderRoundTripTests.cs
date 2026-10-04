using System.Collections.Immutable;
using Turbo.LoadBots.Protocol;
using Turbo.LoadBots.Protocol.Decoders;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.LoadBots;

/// <summary>
/// Real server composers, serialized by the revision, read back by the load bots' decoders. A
/// serializer that gains, loses or reorders a field breaks the decoded values or leaves bytes
/// over, and the bot decoders have to follow before CI goes green again.
/// </summary>
public class BotDecoderRoundTripTests
{
    [Fact]
    public void ActionEditor_DecodesWithItsDelay()
    {
        var (header, reader) = Serialize(
            new WiredFurniActionEventMessageComposer
            {
                WiredData = Wired(WiredType.Action, definitionSpecifics: [7], typeSpecifics: []),
            }
        );

        var kind = WiredDecoders.KindOf(header);
        Assert.Equal(WiredKind.Action, kind);

        var box = WiredDecoders.Box(kind!.Value, reader);

        AssertCommonBox(box, WiredKind.Action);
        Assert.Equal(7, box.DefinitionInt);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ConditionEditor_DecodesWithItsQuantifierAndSkipsItsTypeSpecifics()
    {
        var (header, reader) = Serialize(
            new WiredFurniConditionEventMessageComposer
            {
                WiredData = Wired(
                    WiredType.Condition,
                    definitionSpecifics: [2],
                    typeSpecifics: [(byte)1, true]
                ),
            }
        );

        var kind = WiredDecoders.KindOf(header);
        Assert.Equal(WiredKind.Condition, kind);

        var box = WiredDecoders.Box(kind!.Value, reader);

        AssertCommonBox(box, WiredKind.Condition);
        Assert.Equal(2, box.DefinitionInt);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void SelectorEditor_DecodesWithItsFilterAndInvertFlags()
    {
        var (header, reader) = Serialize(
            new WiredFurniSelectorEventMessageComposer
            {
                WiredData = Wired(
                    WiredType.Selector,
                    definitionSpecifics: [true, false],
                    typeSpecifics: []
                ),
            }
        );

        var kind = WiredDecoders.KindOf(header);
        Assert.Equal(WiredKind.Selector, kind);

        var box = WiredDecoders.Box(kind!.Value, reader);

        AssertCommonBox(box, WiredKind.Selector);
        Assert.True(box.SelectorFilter);
        Assert.False(box.SelectorInvert);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Users_DecodesAPlayerAvatar()
    {
        var (_, reader) = Serialize(new UsersMessageComposer { Avatars = [Player()] });

        var avatar = Assert.Single(RoomDecoders.Users(reader));

        Assert.Equal(1001, avatar.WebId);
        Assert.Equal("LoadBot01", avatar.Name);
        Assert.Equal(3, avatar.ObjectId);
        Assert.Equal(4, avatar.X);
        Assert.Equal(5, avatar.Y);
        Assert.Equal(1.5, avatar.Z);
        Assert.Equal((int)Rotation.South, avatar.BodyRotation);
        Assert.Equal(RoomObjectType.Player, avatar.Type);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void UserUpdate_DecodesPositionAndStatus()
    {
        var (_, reader) = Serialize(new UserUpdateMessageComposer { Avatars = [Player()] });

        var status = Assert.Single(RoomDecoders.UserUpdate(reader));

        Assert.Equal(3, status.ObjectId);
        Assert.Equal(4, status.X);
        Assert.Equal(5, status.Y);
        Assert.Equal(1.5, status.Z);
        Assert.Equal("/flatctrl 4/mv 5,6,1.5/", status.Status);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ObjectAdd_DecodesAFloorItemWithLegacyStuffData()
    {
        var (_, reader) = Serialize(
            new ObjectAddMessageComposer
            {
                FloorItem = FloorItem(77, new LegacyStuffSnapshot { StuffBitmask = 0, Data = "1" }),
            }
        );

        var item = RoomDecoders.ObjectAdd(reader);

        AssertFloorItem(item, 77, "1");
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ObjectAdd_DecodesALimitedFloorItem()
    {
        var (_, reader) = Serialize(
            new ObjectAddMessageComposer
            {
                FloorItem = FloorItem(
                    78,
                    new LegacyStuffSnapshot
                    {
                        StuffBitmask = (int)StuffDataFlags.Unique,
                        Data = "0",
                        UniqueNumber = 12,
                        UniqueSeries = 100,
                    }
                ),
            }
        );

        var item = RoomDecoders.ObjectAdd(reader);

        AssertFloorItem(item, 78, "0");
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Objects_SkipsOwnerNamesAndDecodesEveryItem()
    {
        var (_, reader) = Serialize(
            new ObjectsMessageComposer
            {
                OwnerNames = ImmutableDictionary<PlayerId, string>.Empty.Add(1001, "LoadBot01"),
                FloorItems =
                [
                    FloorItem(80, new LegacyStuffSnapshot { StuffBitmask = 0, Data = "2" }),
                    FloorItem(81, new EmptyStuffSnapshot { StuffBitmask = 4 }),
                ],
            }
        );

        var items = RoomDecoders.Objects(reader);

        Assert.Equal(2, items.Count);
        AssertFloorItem(items[0], 80, "2");
        AssertFloorItem(items[1], 81, string.Empty);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void FurniListAddOrUpdate_DecodesFloorAndWallItems()
    {
        var (_, reader) = Serialize(
            new FurniListAddOrUpdateEventMessageComposer
            {
                Items =
                [
                    InventoryItem(501, 3010, ProductType.Floor),
                    InventoryItem(502, 4020, ProductType.Wall),
                ],
            }
        );

        var items = InventoryDecoders.FurniListAddOrUpdate(reader);

        Assert.Equal(2, items.Count);
        Assert.Equal(501, items[0].ItemId);
        Assert.True(items[0].IsFloor);
        Assert.Equal(3010, items[0].SpriteId);
        Assert.Equal(502, items[1].ItemId);
        Assert.False(items[1].IsFloor);
        Assert.Equal(4020, items[1].SpriteId);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void CatalogPage_DecodesTheOfferAndItsFloorProduct()
    {
        var offer = Offer();
        var (_, reader) = Serialize(
            new CatalogPageMessageComposer
            {
                CatalogType = CatalogType.Normal,
                Page = new CatalogPageSnapshot
                {
                    Id = 15,
                    ParentId = -1,
                    Localization = "Bots",
                    Icon = 1,
                    Layout = "default_3x3",
                    ImageData = ["header.png"],
                    TextData = ["Some text"],
                    Visible = true,
                    OfferIds = [offer.Id],
                    ChildIds = [],
                },
                Offers = [offer],
                OfferProducts = ImmutableDictionary<
                    int,
                    ImmutableArray<CatalogProductSnapshot>
                >.Empty,
                OfferId = -1,
                AcceptSeasonCurrencyAsCredits = false,
                FrontPageItems = [],
            }
        );

        var page = CatalogDecoders.CatalogPage(reader);

        Assert.Equal(15, page.PageId);
        Assert.Equal("default_3x3", page.Layout);
        AssertOffer(Assert.Single(page.Offers));

        // The decoder stops after the offers; what is left is the page's tail, field for field.
        Assert.Equal(-1, reader.Int()); // offer id
        Assert.False(reader.Bool()); // season currency as credits
        Assert.Equal(0, reader.Count()); // front page items
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void PurchaseOk_DecodesTheOfferAsBought()
    {
        var (_, reader) = Serialize(new PurchaseOKMessageComposer { Offer = Offer() });

        AssertOffer(CatalogDecoders.PurchaseOk(reader));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void NavigatorSearchResultBlocks_DecodesTheRoomInTheBlock()
    {
        var (_, reader) = Serialize(
            new NavigatorSearchResultBlocksMessageComposer
            {
                SearchCodeOriginal = "hotel_view",
                FilteringData = string.Empty,
                Blocks =
                [
                    new NavigatorSearchResultBlockSnapshot
                    {
                        SearchCode = "popular",
                        Text = "Popular",
                        ActionAllowed = NavigatorActionAllowedType.Expanded,
                        Localization = string.Empty,
                        ForceClosed = false,
                        ViewMode = NavigatorViewModeType.Rows,
                        Results =
                        [
                            new NavigatorSearchResultSnapshot
                            {
                                RoomId = 42,
                                Name = "Bot room",
                                Description = "For bots",
                                OwnerId = 1001,
                                OwnerName = "LoadBot01",
                                Population = 3,
                                LastUpdatedUtc = DateTime.UnixEpoch,
                                DoorMode = RoomDoorModeType.Open,
                                PlayersMax = 25,
                                TradeType = RoomTradeModeType.Disabled,
                                Score = 0,
                                Ranking = 0,
                                CategoryId = 1,
                                Tags = ["bots", "load"],
                                AllowBlocking = false,
                                AllowPets = true,
                                AllowPetsEat = false,
                            },
                        ],
                    },
                ],
            }
        );

        var room = Assert.Single(NavigatorDecoders.SearchResultBlocks(reader));

        Assert.Equal(42, room.RoomId);
        Assert.Equal("Bot room", room.Name);
        Assert.Equal(1001, room.OwnerId);
        Assert.Equal("LoadBot01", room.OwnerName);
        Assert.Equal((int)RoomDoorModeType.Open, room.DoorMode);
        Assert.Equal(3, room.Users);
        Assert.Equal(25, room.MaxUsers);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void Chat_DecodesTheLeadingFields()
    {
        var (_, reader) = Serialize(
            new ChatMessageComposer
            {
                ObjectId = 3,
                Text = "hello bots",
                Gesture = AvatarGestureType.Smile,
                StyleId = 7,
                Links = [],
                TrackingId = 11,
            }
        );

        AssertChat(RoomDecoders.Chat(reader), reader);
    }

    [Fact]
    public void Whisper_DecodesTheLeadingFieldsLikeChat()
    {
        var (_, reader) = Serialize(
            new WhisperMessageComposer
            {
                ObjectId = 3,
                Text = "hello bots",
                Gesture = AvatarGestureType.Smile,
                StyleId = 7,
                Links = [],
                TrackingId = 11,
            }
        );

        AssertChat(RoomDecoders.Chat(reader), reader);
    }

    [Fact]
    public void FloorHeightMap_DecodesTheScaleWallHeightAndModel()
    {
        var (_, reader) = Serialize(
            new FloorHeightMapMessageComposer
            {
                ScaleType = RoomScaleType.Small,
                FixedWallsHeight = 2,
                ModelData = "xxx\r000\r000",
                AreaHideData = [],
                CameraInitX = 1,
                CameraInitY = 2,
                CameraInitZ = 0.5,
            }
        );

        var plan = RoomDecoders.FloorHeightMap(reader);

        Assert.True(plan.Small);
        Assert.Equal(2, plan.FixedWallsHeight);
        Assert.Equal("xxx\r000\r000", plan.ModelData);

        // The decoder stops after the model; the camera fields follow.
        Assert.Equal(0, reader.Count()); // area hide data
        Assert.Equal(1, reader.Int());
        Assert.Equal(2, reader.Int());
        Assert.Equal(0.5f, reader.Float());
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void HeightMap_DecodesEveryTile()
    {
        short[] heights = [-1, 0, 256, 512, -1, 0];
        var (_, reader) = Serialize(
            new HeightMapMessageComposer
            {
                Width = 3,
                Size = heights.Length,
                Heights = heights,
            }
        );

        var map = RoomDecoders.HeightMap(reader);

        Assert.Equal(3, map.Width);
        Assert.Equal(2, map.Length);
        Assert.Equal(heights, map.Heights);
        Assert.False(map.IsTile(0, 0));
        Assert.True(map.IsTile(1, 0));
        Assert.Equal(0, reader.Remaining);
    }

    /// <summary>The composer through the revision's serializer: its header and a bot reader over the body.</summary>
    private static (int Header, PacketReader Reader) Serialize(IComposer composer)
    {
        var packet = PacketHarness.Encode(composer);
        var body = packet.PopBytes(packet.Remaining);

        return (packet.Header, new PacketReader(body));
    }

    private static WiredDataSnapshot Wired(
        WiredType type,
        List<object> definitionSpecifics,
        List<object> typeSpecifics
    ) =>
        new()
        {
            WiredType = type,
            FurniLimit = 20,
            StuffIds = [10, 11],
            StuffIds2 = [12],
            StuffTypeId = 3400,
            Id = 808,
            StringParam = "param",
            IntParams = [1, 2],
            VariableIds = [new WiredVariableId(0), new WiredVariableId(77)],
            FurniSourceTypes =
            [
                [WiredFurniSourceType.SelectedItems],
            ],
            PlayerSourceTypes =
            [
                [WiredPlayerSourceType.TriggeredUser],
            ],
            Code = 4,
            AdvancedMode = true,
            AmountFurniSelections = [],
            AllowWallFurni = true,
            AllowedFurniSources =
            [
                [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
            ],
            AllowedPlayerSources =
            [
                [WiredPlayerSourceType.TriggeredUser],
            ],
            DefaultFurniSources =
            [
                [WiredFurniSourceType.SelectorItems],
            ],
            DefaultPlayerSources =
            [
                [WiredPlayerSourceType.AllRoomUsers],
            ],
            DefinitionSpecifics = definitionSpecifics,
            TypeSpecifics = typeSpecifics,
            ContextSnapshots =
            [
                new WiredVariableAllInRoomSnapshot
                {
                    ContextType = WiredContextType.AllVariablesInRoom,
                    AllVariablesHash = new WiredVariableHash(12345),
                },
            ],
            DefaultIntParams = [9, 8, 7],
        };

    private static void AssertCommonBox(WiredBox box, WiredKind kind)
    {
        Assert.Equal(kind, box.Kind);
        Assert.Equal(808, box.ObjectId);
        Assert.Equal(3400, box.SpriteId);
        Assert.Equal(20, box.FurniLimit);
        Assert.Equal([10, 11], box.StuffIds);
        Assert.Equal([12], box.StuffIds2);
        Assert.Equal("param", box.StringParam);
        Assert.Equal([1, 2], box.IntParams);
        Assert.Equal([string.Empty, "77"], box.VariableIds);
        Assert.Equal(
            [(int)WiredFurniSourceTypeExtensions.GetProtocolId(WiredFurniSourceType.SelectedItems)],
            box.FurniSources
        );
        Assert.Equal(
            [
                (int)
                    WiredPlayerSourceTypeExtensions.GetProtocolId(
                        WiredPlayerSourceType.TriggeredUser
                    ),
            ],
            box.UserSources
        );
        Assert.Equal(4, box.Code);
        var allowedFurni = Assert.Single(box.AllowedFurniSources);
        Assert.Equal(
            [
                (int)
                    WiredFurniSourceTypeExtensions.GetProtocolId(
                        WiredFurniSourceType.SelectedItems
                    ),
                (int)
                    WiredFurniSourceTypeExtensions.GetProtocolId(
                        WiredFurniSourceType.SelectorItems
                    ),
            ],
            allowedFurni
        );
        Assert.Equal(
            [
                (int)
                    WiredPlayerSourceTypeExtensions.GetProtocolId(
                        WiredPlayerSourceType.TriggeredUser
                    ),
            ],
            Assert.Single(box.AllowedUserSources)
        );
        Assert.Equal(
            [(int)WiredFurniSourceTypeExtensions.GetProtocolId(WiredFurniSourceType.SelectorItems)],
            box.DefaultFurniSources
        );
        Assert.Equal(
            [
                (int)
                    WiredPlayerSourceTypeExtensions.GetProtocolId(
                        WiredPlayerSourceType.AllRoomUsers
                    ),
            ],
            box.DefaultUserSources
        );
        Assert.True(box.AllowWallFurni);
        Assert.Equal([9, 8, 7], box.DefaultIntParams);
    }

    private static RoomPlayerAvatarSnapshot Player() =>
        new()
        {
            AvatarType = RoomObjectType.Player,
            WebId = 1001,
            Name = "LoadBot01",
            Motto = "beep",
            Figure = "hd-180-1",
            ObjectId = 3,
            X = 4,
            Y = 5,
            Z = 1.5,
            BodyRotation = Rotation.South,
            HeadRotation = Rotation.SouthEast,
            JumpPower = 0,
            Status = "/flatctrl 4/mv 5,6,1.5/",
            DanceType = AvatarDanceType.None,
            EffectId = 0,
            IsIdle = false,
            Gender = AvatarGenderType.Female,
            GroupId = 0,
            GroupStatus = GuildMembershipStatus.None,
            GroupName = string.Empty,
            SwimFigure = string.Empty,
            ActivityPoints = 0,
            IsModerator = false,
            BadgesRank = 0,
        };

    private static RoomFloorItemSnapshot FloorItem(int objectId, StuffDataSnapshot stuffData) =>
        new()
        {
            ObjectId = objectId,
            OwnerId = 1001,
            OwnerName = "LoadBot01",
            DefinitionId = 9,
            SpriteId = 3010,
            X = 6,
            Y = 7,
            Z = 0.25,
            Rotation = Rotation.East,
            StackHeight = 1,
            StuffData = stuffData,
            ExtraData = string.Empty,
            UsagePolicy = FurnitureUsageType.Everybody,
        };

    private static void AssertFloorItem(FloorItem item, int objectId, string state)
    {
        Assert.Equal(objectId, item.ObjectId);
        Assert.Equal(3010, item.SpriteId);
        Assert.Equal(6, item.X);
        Assert.Equal(7, item.Y);
        Assert.Equal((int)Rotation.East, item.Rotation);
        Assert.Equal(0.25, item.Z);
        Assert.Equal(state, item.State);
        Assert.Equal(1001, item.OwnerId);
    }

    private static FurnitureItemSnapshot InventoryItem(
        int itemId,
        int spriteId,
        ProductType type
    ) =>
        new()
        {
            ItemId = itemId,
            SpriteId = spriteId,
            OwnerId = 1001,
            OwnerName = "LoadBot01",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 9,
                SpriteId = spriteId,
                Name = "bot_furni",
                ProductType = type,
                FurniCategory = FurnitureCategory.Default,
                LogicName = "default",
                TotalStates = 2,
                Width = 1,
                Length = 1,
                StackHeight = 1,
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = true,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Everybody,
                ExtraData = null,
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = 0,
            SlotId = string.Empty,
            Extra = 0,
        };

    private static CatalogOfferSnapshot Offer() =>
        new()
        {
            Id = 301,
            PageId = 15,
            LocalizationId = "bot_chair",
            Rentable = false,
            CostCredits = 3,
            CostSilver = 0,
            CostCurrency = 5,
            CurrencyTypeId = 0,
            CanGift = true,
            CanBundle = true,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [1],
            Products =
            [
                new CatalogProductSnapshot
                {
                    Id = 1,
                    OfferId = 301,
                    ProductType = ProductType.Floor,
                    FurniDefinitionId = 9,
                    SpriteId = 3010,
                    ExtraParam = null,
                    Quantity = 1,
                    UniqueSize = 0,
                    UniqueRemaining = 0,
                    ClassName = "bot_chair",
                },
            ],
        };

    private static void AssertOffer(CatalogOffer offer)
    {
        Assert.Equal(301, offer.OfferId);
        Assert.Equal(3, offer.CostCredits);
        Assert.Equal(5, offer.CostCurrency);
        Assert.Equal(0, offer.ClubLevel);
        var product = Assert.Single(offer.Products);
        Assert.Equal(ProductType.Floor, product.Type);
        Assert.Equal(3010, product.SpriteId);
        Assert.Equal(1, product.Quantity);
    }

    private static void AssertChat(ChatLine line, PacketReader reader)
    {
        Assert.Equal(3, line.ObjectId);
        Assert.Equal("hello bots", line.Text);
        Assert.Equal(7, line.StyleId);

        // The decoder stops after the style; the links and tracking id follow.
        Assert.Equal(0, reader.Count()); // links
        Assert.Equal(11, reader.Int()); // tracking id
        Assert.Equal(0, reader.Remaining);
    }
}
