using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public Altitude MaxStackHeight { get; init; } = Altitude.FromInt(4000);
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
    public Altitude MaxStepHeight { get; init; } = Altitude.FromInt(200);
    public bool PlaceItemsOnAvatars { get; init; } = true;
    public bool EnableDiagonalChecking { get; init; } = true;
    public int MaxPlayersLimit { get; init; } = 50;
    public int RoomNameMaxLength { get; init; } = 60;
    public int RoomDescriptionMaxLength { get; init; } = 128;
    public int RoomPasswordMaxLength { get; init; } = 64;
    public int RoomTagsMax { get; init; } = 2;
    public int RoomTagMaxLength { get; init; } = 30;
    public int RoomIdleSleepTimeoutMinSeconds { get; init; } = 60;
    public int RoomIdleSleepTimeoutMaxSeconds { get; init; } = 3600;
    public int RoomIdleAutokickTimeoutMinSeconds { get; init; } = 60;
    public int RoomIdleAutokickTimeoutMaxSeconds { get; init; } = 86400;

    /// <summary>
    /// Listing changes the room directory remembers for navigator caches. A silo that falls
    /// further behind than this drops its whole cache instead.
    /// </summary>
    public int ListingChangeLogSize { get; init; } = 10000;

    public int RoomCheckMs { get; init; } = 300000;
    public int RoomDeactivationDelayMs { get; init; } = 1800000;
    public int RoomTickMs { get; init; } = 50;
    public int AvatarTickMs { get; init; } = 500;
    public int RollerTickMs { get; init; } = 2000;
    public int WiredTickMs { get; init; } = 50;
    public int DirtyItemsTickMs { get; init; } = 2000;
    public int MaxDirtyItemsPerFlush { get; init; } = 100;
    public int MaxTileHeightsPerFlush { get; init; } = 200;
    public int MaxPathNodes { get; init; } = 4096;

    public int ChatMaxLength { get; init; } = 100;
    public int ChatLookAtRange { get; init; } = 6;
    public int ChatFloodMaxMessagesExtraSensitivity { get; init; } = 4;
    public int ChatFloodMaxMessagesNormalSensitivity { get; init; } = 6;
    public int ChatFloodMaxMessagesMinimalSensitivity { get; init; } = 8;
    public int ChatFloodWindowMs { get; init; } = 4000;
    public int ChatFloodMuteMs { get; init; } = 30000;
    public int ChatMuteMaxDurationMinutes { get; init; } = 60;
    public bool ChatlogEnabled { get; init; } = true;
    public int ChatlogTickMs { get; init; } = 5000;
    public int MaxChatlogsPerFlush { get; init; } = 200;
    public int MaxPendingChatlogs { get; init; } = 2000;

    public int WiredMaxDepth { get; init; } = 20;
    public int WiredMaxScheduledPerTick { get; init; } = 64;
    public int WiredMaxEventsPerTick { get; init; } = 64;
    public int WiredSelectorMaxAreaSize { get; init; } = 100;
    public int WiredSelectedItemsLimit { get; init; } = 20;
    public bool WiredAllowWallFurni { get; init; } = true;
    public int WiredMaxIntParams { get; init; } = 16;
    public int WiredNeighborhoodRadius { get; init; } = 5;
    public int WiredMaxFloorItems { get; init; } = 200;
    public int WiredMaxWallItems { get; init; } = 50;
    public int WiredMaxPermanentFurniVariables { get; init; } = 50;
    public int WiredMaxPermanentUserVariables { get; init; } = 50;
    public int WiredMaxPermanentGlobalVariables { get; init; } = 50;
    public int WiredExecutionCostWindowMs { get; init; } = 1000;
    public int WiredExecutionCostCap { get; init; } = 500;
    public int WiredMaxErrorLogEntries { get; init; } = 50;
}
