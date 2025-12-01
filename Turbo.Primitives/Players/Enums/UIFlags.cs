using System;

namespace Turbo.Primitives.Players.Enums;

[Flags]
public enum UIFlags
{
    None = 0,
    FriendBarExpanded = 1 << 0,
    RoomToolsExpanded = 1 << 1,
}
