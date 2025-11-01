using System;

namespace Turbo.Contracts.Enums.Navigator;

[Flags]
public enum RoomBitmask
{
    None = 0,
    Thumbnail = 1 << 0,
    GroupData = 1 << 1,
    RoomAd = 1 << 2,
    ShowOwner = 1 << 3,
    AllowPets = 1 << 4,
    DisplayRoomAd = 1 << 5,
}
