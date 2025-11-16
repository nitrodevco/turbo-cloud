using System.Collections.Generic;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Navigator;
using Turbo.Contracts.Enums.Navigator.Chat;
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
    public RoomTradeModeType TradeMode { get; init; }
    public bool AllowPets { get; init; }
    public bool AllowFoodConsume { get; init; }
    public bool AllowWalkThrough { get; init; }
    public bool HideWalls { get; init; }
    public RoomThicknessType WallThickness { get; init; }
    public RoomThicknessType FloorThickness { get; init; }
    public ModSettingType WhoCanMute { get; init; }
    public ModSettingType WhoCanKick { get; init; }
    public ModSettingType WhoCanBan { get; init; }
    public ChatModeType ChatMode { get; init; }
    public ChatBubbleWidthType ChatBubbleSize { get; init; }
    public ChatScrollSpeedType ChatScrollUpFrequency { get; init; }
    public int ChatFullHearRange { get; init; }
    public ChatFloodSensitivityType ChatFloodSensitivity { get; init; }
    public bool AllowNavigatorDynCats { get; init; }
}
