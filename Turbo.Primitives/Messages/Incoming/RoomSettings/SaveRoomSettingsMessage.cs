using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record SaveRoomSettingsMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public string RoomDescription { get; init; } = string.Empty;
    public int DoorMode { get; init; }
    public string Password { get; init; } = string.Empty;
    public int MaxVisitors { get; init; }
    public int CategoryId { get; init; }
    public List<string> Tags { get; init; } = new();
    public RoomTradeType TradeMode { get; init; }
    public bool AllowPets { get; init; }
    public bool AllowFoodConsume { get; init; }
    public bool AllowWalkThrough { get; init; }
    public bool HideWalls { get; init; }
    public RoomThicknessType WallThickness { get; init; }
    public RoomThicknessType FloorThickness { get; init; }
    public RoomMuteType WhoCanMute { get; init; }
    public RoomKickType WhoCanKick { get; init; }
    public RoomBanType WhoCanBan { get; init; }
    public RoomChatModeType ChatMode { get; init; }
    public RoomChatWeightType ChatBubbleSize { get; init; }
    public RoomChatSpeedType ChatScrollUpFrequency { get; init; }
    public int ChatFullHearRange { get; init; }
    public RoomChatProtectionType ChatFloodSensitivity { get; init; }
    public bool AllowNavigatorDynCats { get; init; }
}
