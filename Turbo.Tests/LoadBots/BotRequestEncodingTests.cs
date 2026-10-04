using Turbo.LoadBots.Protocol;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.LoadBots;

/// <summary>
/// Every message the load bots send, run through the server revision's own parser. If a parser
/// changes its layout and the bot does not follow, the parsed values (or the whole-body check)
/// stop matching here rather than on a live load run.
/// </summary>
public class BotRequestEncodingTests
{
    [Fact]
    public void ClientHello_ParsesAsTheProductionAndFlashPlatform()
    {
        var parsed = ParseWhole<ClientHelloMessage>(
            ClientRequests.ClientHello("PRODUCTION-1"),
            "ClientHelloMessageEvent"
        );

        Assert.Equal("PRODUCTION-1", parsed.Production);
        Assert.Equal("FLASH", parsed.Platform);
        Assert.Equal(1, parsed.ClientPlatform);
        Assert.Equal(0, parsed.DeviceCategory);
    }

    [Fact]
    public void SsoTicket_ParsesAsTheTicketAndElapsedTime()
    {
        var parsed = ParseWhole<SSOTicketMessage>(
            ClientRequests.SsoTicket("ticket-abc", 1234),
            "SSOTicketMessageEvent"
        );

        Assert.Equal("ticket-abc", parsed.SSO);
        Assert.Equal(1234, parsed.ElapsedMilliseconds);
    }

    [Fact]
    public void CreateFlat_ParsesEveryField()
    {
        var parsed = ParseWhole<CreateFlatMessage>(
            ClientRequests.CreateFlat("Bot room", "A description", "model_a", 3, 25, 2),
            "CreateFlatMessageEvent"
        );

        Assert.Equal("Bot room", parsed.FlatName);
        Assert.Equal("A description", parsed.FlatDescription);
        Assert.Equal("model_a", parsed.FlatModelName);
        Assert.Equal(3, parsed.CategoryID);
        Assert.Equal(25, parsed.MaxPlayers);
        Assert.Equal(RoomTradeModeType.Everyone, parsed.TradeSetting);
    }

    [Fact]
    public void OpenFlatConnection_ParsesTheRoomAndPassword()
    {
        var parsed = ParseWhole<OpenFlatConnectionMessage>(
            ClientRequests.OpenFlatConnection(77, "secret"),
            "OpenFlatConnectionMessageEvent"
        );

        Assert.Equal(77, parsed.RoomId.Value);
        Assert.Equal("secret", parsed.Password);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GetGuestRoom_ParsesTheRoomAndBothFlags(bool enterRoom, bool roomForward)
    {
        var parsed = ParseWhole<GetGuestRoomMessage>(
            ClientRequests.GetGuestRoom(42, enterRoom, roomForward),
            "GetGuestRoomMessageEvent"
        );

        Assert.Equal(42, parsed.RoomId.Value);
        Assert.Equal(enterRoom, parsed.EnterRoom);
        Assert.Equal(roomForward, parsed.RoomForward);
    }

    [Fact]
    public void MoveAvatar_ParsesTheTarget()
    {
        var parsed = ParseWhole<MoveAvatarMessage>(
            ClientRequests.MoveAvatar(5, 9),
            "MoveAvatarMessageEvent"
        );

        Assert.Equal(5, parsed.TargetX);
        Assert.Equal(9, parsed.TargetY);
    }

    [Fact]
    public void Chat_ParsesTheTextStyleAndNoTrackingId()
    {
        var parsed = ParseWhole<ChatMessage>(
            ClientRequests.Chat("héllo there", 7),
            "ChatMessageEvent"
        );

        Assert.Equal("héllo there", parsed.Text);
        Assert.Equal(7, parsed.StyleId);
        Assert.Equal(-1, parsed.TrackingId);
    }

    [Fact]
    public void Shout_ParsesTheTextAndStyle()
    {
        var parsed = ParseWhole<ShoutMessage>(ClientRequests.Shout("LOUD", 3), "ShoutMessageEvent");

        Assert.Equal("LOUD", parsed.Text);
        Assert.Equal(3, parsed.StyleId);
    }

    [Fact]
    public void Dance_ParsesTheDanceId()
    {
        var parsed = ParseWhole<DanceMessage>(ClientRequests.Dance(2), "DanceMessageEvent");

        Assert.Equal(2, parsed.DanceId);
    }

    [Fact]
    public void Sign_ParsesTheSignId()
    {
        var parsed = ParseWhole<SignMessage>(ClientRequests.Sign(11), "SignMessageEvent");

        Assert.Equal(11, parsed.SignType);
    }

    [Fact]
    public void AvatarExpression_ParsesTheExpressionId()
    {
        var parsed = ParseWhole<AvatarExpressionMessage>(
            ClientRequests.AvatarExpression(5),
            "AvatarExpressionMessageEvent"
        );

        Assert.Equal(5, parsed.ExpressionId);
    }

    [Fact]
    public void GetCatalogIndex_AsksForTheNormalCatalog()
    {
        var parsed = ParseWhole<GetCatalogIndexMessage>(
            ClientRequests.GetCatalogIndex(),
            "GetCatalogIndexMessageEvent"
        );

        Assert.Equal(CatalogType.Normal, parsed.CatalogType);
    }

    [Fact]
    public void GetCatalogPage_ParsesThePageWithNoOfferInTheNormalCatalog()
    {
        var parsed = ParseWhole<GetCatalogPageMessage>(
            ClientRequests.GetCatalogPage(15),
            "GetCatalogPageMessageEvent"
        );

        Assert.Equal(15, parsed.PageId);
        Assert.Equal(-1, parsed.OfferId);
        Assert.Equal(CatalogType.Normal, parsed.CatalogType);
    }

    [Fact]
    public void PurchaseFromCatalog_ParsesThePageOfferAndQuantity()
    {
        var parsed = ParseWhole<PurchaseFromCatalogMessage>(
            ClientRequests.PurchaseFromCatalog(15, 301, 4),
            "PurchaseFromCatalogMessageEvent"
        );

        Assert.Equal(15, parsed.PageId);
        Assert.Equal(301, parsed.OfferId);
        Assert.Equal(string.Empty, parsed.ExtraParam);
        Assert.Equal(4, parsed.Quantity);
    }

    [Fact]
    public void RequestFurniInventory_IsAnEmptyBodyTheParserAccepts()
    {
        var message = ClientRequests.RequestFurniInventory();

        Assert.Empty(message.Body);
        ParseWhole<RequestFurniInventoryMessage>(message, "RequestFurniInventoryMessageEvent");
    }

    [Fact]
    public void PlaceFloorItem_SendsTheItemPositionAndRotationAsOneString()
    {
        var parsed = ParseWhole<PlaceObjectMessage>(
            ClientRequests.PlaceFloorItem(9001, 4, 6, 2),
            "PlaceObjectMessageEvent"
        );

        Assert.Equal("9001 4 6 2", parsed.Data);
    }

    [Fact]
    public void MoveObject_ParsesTheObjectPositionAndRotation()
    {
        var parsed = ParseWhole<MoveObjectMessage>(
            ClientRequests.MoveObject(55, 3, 8, 4),
            "MoveObjectMessageEvent"
        );

        Assert.Equal(55, parsed.ObjectId.Value);
        Assert.Equal(3, parsed.X);
        Assert.Equal(8, parsed.Y);
        Assert.Equal(Rotation.South, parsed.Rotation);
    }

    [Fact]
    public void PickupFloorItem_ParsesAsAnUnconfirmedFloorPickup()
    {
        var parsed = ParseWhole<PickupObjectMessage>(
            ClientRequests.PickupFloorItem(55),
            "PickupObjectMessageEvent"
        );

        Assert.Equal(ClientRequests.PICKUP_CATEGORY_FLOOR, parsed.CategoryId);
        Assert.Equal(55, parsed.ObjectId.Value);
        Assert.False(parsed.Confirm);
    }

    [Fact]
    public void UseFurniture_ParsesTheObjectAndParam()
    {
        var parsed = ParseWhole<UseFurnitureMessage>(
            ClientRequests.UseFurniture(55, 1),
            "UseFurnitureMessageEvent"
        );

        Assert.Equal(55, parsed.ObjectId.Value);
        Assert.Equal(1, parsed.Param);
    }

    [Fact]
    public void UpdateFloorProperties_ParsesAllSevenFields()
    {
        var parsed = ParseWhole<UpdateFloorPropertiesMessage>(
            ClientRequests.UpdateFloorProperties("xxx\r000\r000", 1, 2, 4, 1, -1, 3),
            "UpdateFloorPropertiesMessageEvent"
        );

        Assert.Equal("xxx\r000\r000", parsed.ModelData);
        Assert.True(parsed.HasProperties);
        Assert.Equal(1, parsed.DoorX);
        Assert.Equal(2, parsed.DoorY);
        Assert.Equal(4, parsed.DoorRotation);
        Assert.Equal(1, parsed.WallThickness);
        Assert.Equal(-1, parsed.FloorThickness);
        Assert.Equal(3, parsed.FixedWallsHeight);
    }

    [Fact]
    public void NavigatorSearch_ParsesTheSearchCodeAndFilter()
    {
        var parsed = ParseWhole<NewNavigatorSearchMessage>(
            ClientRequests.NavigatorSearch("hotel_view", "owner:bot"),
            "NewNavigatorSearchMessageEvent"
        );

        Assert.Equal("hotel_view", parsed.SearchCodeOriginal);
        Assert.Equal("owner:bot", parsed.FilteringData);
    }

    [Fact]
    public void OpenWired_ParsesTheObjectId()
    {
        var parsed = ParseWhole<OpenMessage>(ClientRequests.OpenWired(808), "OpenMessageEvent");

        Assert.Equal(808, parsed.Id);
    }

    [Theory]
    [InlineData(WiredKind.Trigger, "UpdateTriggerMessageEvent", typeof(UpdateTriggerMessage))]
    [InlineData(WiredKind.Action, "UpdateActionMessageEvent", typeof(UpdateActionMessage))]
    [InlineData(WiredKind.Condition, "UpdateConditionMessageEvent", typeof(UpdateConditionMessage))]
    [InlineData(WiredKind.Selector, "UpdateSelectorMessageEvent", typeof(UpdateSelectorMessage))]
    [InlineData(WiredKind.Addon, "UpdateAddonMessageEvent", typeof(UpdateAddonMessage))]
    [InlineData(WiredKind.Variable, "UpdateVariableMessageEvent", typeof(UpdateVariableMessage))]
    public void UpdateWired_ParsesEveryFieldForEachKind(
        WiredKind kind,
        string headerName,
        Type messageType
    )
    {
        int[] furniSources =
        [
            (int)WiredFurniSourceTypeExtensions.GetProtocolId(WiredFurniSourceType.SelectedItems),
            (int)WiredFurniSourceTypeExtensions.GetProtocolId(WiredFurniSourceType.SelectorItems),
        ];
        int[] userSources =
        [
            (int)WiredPlayerSourceTypeExtensions.GetProtocolId(WiredPlayerSourceType.TriggeredUser),
            (int)WiredPlayerSourceTypeExtensions.GetProtocolId(WiredPlayerSourceType.AllRoomUsers),
        ];
        var save = new WiredSave
        {
            Kind = kind,
            ObjectId = 4242,
            IntParams = [1, 2, 3],
            StringParam = "bot:text",
            StuffIds = [10, 11],
            StuffIds2 = [20],
            Delay = 5,
            Quantifier = 1,
            Filter = true,
            Invert = false,
            FurniSources = furniSources,
            UserSources = userSources,
            VariableIds = ["123", "456"],
        };

        var message = ClientRequests.UpdateWired(save);
        var parsed = ParseWhole<UpdateWiredMessage>(message, headerName);

        Assert.IsType(messageType, parsed);
        Assert.Equal(4242, parsed.Id);
        Assert.Equal([1, 2, 3], parsed.IntParams);
        Assert.Equal("bot:text", parsed.StringParam);
        Assert.Equal([10, 11], parsed.StuffIds);
        Assert.Equal([20], parsed.StuffIds2);
        Assert.Equal(
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
            parsed.FurniSources.Select(s => Assert.Single(s))
        );
        Assert.Equal(
            furniSources,
            parsed.FurniSources.Select(s =>
                (int)WiredFurniSourceTypeExtensions.GetProtocolId(Assert.Single(s))
            )
        );
        Assert.Equal(
            [WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.AllRoomUsers],
            parsed.PlayerSources.Select(s => Assert.Single(s))
        );
        Assert.Equal(
            userSources,
            parsed.PlayerSources.Select(s =>
                (int)WiredPlayerSourceTypeExtensions.GetProtocolId(Assert.Single(s))
            )
        );
        Assert.Equal(["123", "456"], parsed.VariableIds);
        Assert.Empty(parsed.TypeSpecifics);

        object[] expectedSpecifics = kind switch
        {
            WiredKind.Action => [5],
            WiredKind.Condition => [1],
            WiredKind.Selector => [true, false],
            _ => [],
        };
        Assert.Equal(expectedSpecifics, parsed.DefinitionSpecifics);
    }

    [Fact]
    public void UpdateHeader_IsTheRevisionsUpdateMessageForEachKind()
    {
        Assert.Equal(
            PacketHarness.Incoming("UpdateTriggerMessageEvent"),
            ClientRequests.UpdateHeader(WiredKind.Trigger)
        );
        Assert.Equal(
            PacketHarness.Incoming("UpdateActionMessageEvent"),
            ClientRequests.UpdateHeader(WiredKind.Action)
        );
        Assert.Equal(
            PacketHarness.Incoming("UpdateConditionMessageEvent"),
            ClientRequests.UpdateHeader(WiredKind.Condition)
        );
        Assert.Equal(
            PacketHarness.Incoming("UpdateSelectorMessageEvent"),
            ClientRequests.UpdateHeader(WiredKind.Selector)
        );
        Assert.Equal(
            PacketHarness.Incoming("UpdateAddonMessageEvent"),
            ClientRequests.UpdateHeader(WiredKind.Addon)
        );
        Assert.Equal(
            PacketHarness.Incoming("UpdateVariableMessageEvent"),
            ClientRequests.UpdateHeader(WiredKind.Variable)
        );
    }

    /// <summary>
    /// The bot's message through the parser the revision registers for its header, the same
    /// call <see cref="PacketHarness.Parse"/> makes, but holding the packet so the test can see
    /// the parser read every byte the bot wrote.
    /// </summary>
    private static T ParseWhole<T>(ClientMessage message, string headerName)
        where T : IMessageEvent
    {
        Assert.Equal(PacketHarness.Incoming(headerName), message.Header);

        var packet = new ClientPacket(message.Header, message.Body);
        var parsed = PacketHarness.Revision.Parsers[message.Header].Parse(packet);

        Assert.True(packet.End, $"{headerName}: {packet.Remaining} bytes left unread.");

        return Assert.IsAssignableFrom<T>(parsed);
    }
}
