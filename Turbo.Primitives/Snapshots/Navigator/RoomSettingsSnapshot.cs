using System.Collections.Generic;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Snapshots.Navigator;

public sealed record RoomSettingsSnapshot(
    long Id,
    string Name,
    int OwnerId,
    string OwnerName,
    DoorModeType DoorMode,
    int UserCount,
    int MaxUserCount,
    string Description,
    TradeModeType TradeMode,
    int Score,
    int Ranking,
    int CategoryId,
    List<string> Tags,
    RoomBitmask Bitmask
);
