using System.Collections.Generic;
using System.Globalization;
using Turbo.Revisions.Revision20260909;

namespace Turbo.LoadBots.Protocol;

/// <summary>One message for the server: its header id and payload.</summary>
public sealed record ClientMessage(int Header, byte[] Body);

/// <summary>
/// The client messages the bots send, each laid out field for field as the revision's parser
/// reads it (<c>Turbo.Revisions/Revision20260909/Parsers</c>).
/// </summary>
public static class ClientRequests
{
    public const string CATALOG_NORMAL = "NORMAL";

    /// <summary>The room object category the client sends to pick up a floor item.</summary>
    public const int PICKUP_CATEGORY_FLOOR = 10;

    public static ClientMessage ClientHello(string production) =>
        new(
            MessageEvent.ClientHelloMessageEvent,
            new PacketWriter().String(production).String("FLASH").Int(1).Int(0).ToArray()
        );

    public static ClientMessage SsoTicket(string ticket, int elapsedMs) =>
        new(
            MessageEvent.SSOTicketMessageEvent,
            new PacketWriter().String(ticket).Int(elapsedMs).ToArray()
        );

    public static ClientMessage Pong() => new(MessageEvent.PongMessageEvent, []);

    public static ClientMessage CreateFlat(
        string name,
        string description,
        string model,
        int categoryId,
        int maxUsers,
        int tradeSetting
    ) =>
        new(
            MessageEvent.CreateFlatMessageEvent,
            new PacketWriter()
                .String(name)
                .String(description)
                .String(model)
                .Int(categoryId)
                .Int(maxUsers)
                .Int(tradeSetting)
                .ToArray()
        );

    public static ClientMessage OpenFlatConnection(int roomId, string password = "") =>
        new(
            MessageEvent.OpenFlatConnectionMessageEvent,
            new PacketWriter().Int(roomId).String(password).Int(-1).ToArray()
        );

    public static ClientMessage GetGuestRoom(int roomId, bool enterRoom, bool roomForward) =>
        new(
            MessageEvent.GetGuestRoomMessageEvent,
            new PacketWriter().Int(roomId).Int(enterRoom ? 1 : 0).Int(roomForward ? 1 : 0).ToArray()
        );

    public static ClientMessage MoveAvatar(int x, int y) =>
        new(MessageEvent.MoveAvatarMessageEvent, new PacketWriter().Int(x).Int(y).ToArray());

    public static ClientMessage Chat(string text, int styleId = 0) =>
        new(
            MessageEvent.ChatMessageEvent,
            new PacketWriter().String(text).Int(styleId).Int(-1).ToArray()
        );

    public static ClientMessage Shout(string text, int styleId = 0) =>
        new(MessageEvent.ShoutMessageEvent, new PacketWriter().String(text).Int(styleId).ToArray());

    public static ClientMessage Dance(int danceId) =>
        new(MessageEvent.DanceMessageEvent, new PacketWriter().Int(danceId).ToArray());

    public static ClientMessage Sign(int signId) =>
        new(MessageEvent.SignMessageEvent, new PacketWriter().Int(signId).ToArray());

    public static ClientMessage AvatarExpression(int expressionId) =>
        new(
            MessageEvent.AvatarExpressionMessageEvent,
            new PacketWriter().Int(expressionId).ToArray()
        );

    public static ClientMessage GetCatalogIndex() =>
        new(
            MessageEvent.GetCatalogIndexMessageEvent,
            new PacketWriter().String(CATALOG_NORMAL).ToArray()
        );

    public static ClientMessage GetCatalogPage(int pageId) =>
        new(
            MessageEvent.GetCatalogPageMessageEvent,
            new PacketWriter().Int(pageId).Int(-1).String(CATALOG_NORMAL).ToArray()
        );

    public static ClientMessage PurchaseFromCatalog(int pageId, int offerId, int quantity = 1) =>
        new(
            MessageEvent.PurchaseFromCatalogMessageEvent,
            new PacketWriter().Int(pageId).Int(offerId).String(string.Empty).Int(quantity).ToArray()
        );

    public static ClientMessage RequestFurniInventory() =>
        new(MessageEvent.RequestFurniInventoryMessageEvent, []);

    public static ClientMessage PlaceFloorItem(int itemId, int x, int y, int rotation) =>
        new(
            MessageEvent.PlaceObjectMessageEvent,
            new PacketWriter()
                .String(string.Create(CultureInfo.InvariantCulture, $"{itemId} {x} {y} {rotation}"))
                .ToArray()
        );

    public static ClientMessage MoveObject(int objectId, int x, int y, int rotation) =>
        new(
            MessageEvent.MoveObjectMessageEvent,
            new PacketWriter().Int(objectId).Int(x).Int(y).Int(rotation).ToArray()
        );

    public static ClientMessage PickupFloorItem(int objectId) =>
        new(
            MessageEvent.PickupObjectMessageEvent,
            new PacketWriter().Int(PICKUP_CATEGORY_FLOOR).Int(objectId).Bool(false).ToArray()
        );

    public static ClientMessage UseFurniture(int objectId, int param = 0) =>
        new(
            MessageEvent.UseFurnitureMessageEvent,
            new PacketWriter().Int(objectId).Int(param).ToArray()
        );

    public static ClientMessage UpdateFloorProperties(
        string modelData,
        int doorX,
        int doorY,
        int doorRotation,
        int wallThickness,
        int floorThickness,
        int fixedWallsHeight
    ) =>
        new(
            MessageEvent.UpdateFloorPropertiesMessageEvent,
            new PacketWriter()
                .String(modelData)
                .Int(doorX)
                .Int(doorY)
                .Int(doorRotation)
                .Int(wallThickness)
                .Int(floorThickness)
                .Int(fixedWallsHeight)
                .ToArray()
        );

    public static ClientMessage NavigatorSearch(string searchCode, string filter) =>
        new(
            MessageEvent.NewNavigatorSearchMessageEvent,
            new PacketWriter().String(searchCode).String(filter).ToArray()
        );

    public static ClientMessage OpenWired(int objectId) =>
        new(MessageEvent.OpenMessageEvent, new PacketWriter().Int(objectId).ToArray());

    /// <summary>
    /// A wired save, in the one layout every Update* message shares
    /// (<c>UpdateWiredDataParser</c>). The definition specifics are what the kind's parser
    /// reads after the picked furni: an action's delay, a condition's quantifier, a selector's
    /// filter and invert flags; the other kinds carry none.
    /// </summary>
    public static ClientMessage UpdateWired(WiredSave save)
    {
        var writer = new PacketWriter()
            .Int(save.ObjectId)
            .Ints(save.IntParams)
            .String(save.StringParam)
            .Ints(save.StuffIds);

        switch (save.Kind)
        {
            case WiredKind.Action:
                writer.Int(save.Delay);
                break;
            case WiredKind.Condition:
                writer.Int(save.Quantifier);
                break;
            case WiredKind.Selector:
                writer.Bool(save.Filter).Bool(save.Invert);
                break;
            default:
                break;
        }

        writer
            .Ints(save.FurniSources)
            .Ints(save.UserSources)
            .Strings(save.VariableIds)
            .Ints(save.StuffIds2);

        return new ClientMessage(UpdateHeader(save.Kind), writer.ToArray());
    }

    public static int UpdateHeader(WiredKind kind) =>
        kind switch
        {
            WiredKind.Trigger => MessageEvent.UpdateTriggerMessageEvent,
            WiredKind.Action => MessageEvent.UpdateActionMessageEvent,
            WiredKind.Condition => MessageEvent.UpdateConditionMessageEvent,
            WiredKind.Selector => MessageEvent.UpdateSelectorMessageEvent,
            WiredKind.Addon => MessageEvent.UpdateAddonMessageEvent,
            WiredKind.Variable => MessageEvent.UpdateVariableMessageEvent,
            _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}

/// <summary>The kinds of wired box, each with its own editor and save message.</summary>
public enum WiredKind
{
    Trigger,
    Action,
    Condition,
    Selector,
    Addon,
    Variable,
}

/// <summary>Everything one wired save carries.</summary>
public sealed record WiredSave
{
    public required WiredKind Kind { get; init; }
    public required int ObjectId { get; init; }
    public IReadOnlyList<int> IntParams { get; init; } = [];
    public string StringParam { get; init; } = string.Empty;
    public IReadOnlyList<int> StuffIds { get; init; } = [];
    public IReadOnlyList<int> StuffIds2 { get; init; } = [];
    public int Delay { get; init; }
    public int Quantifier { get; init; }
    public bool Filter { get; init; }
    public bool Invert { get; init; }
    public IReadOnlyList<int> FurniSources { get; init; } = [];
    public IReadOnlyList<int> UserSources { get; init; } = [];
    public IReadOnlyList<string> VariableIds { get; init; } = [];
}
