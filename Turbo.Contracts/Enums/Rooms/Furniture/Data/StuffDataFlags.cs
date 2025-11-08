using System;

namespace Turbo.Contracts.Enums.Rooms.Furniture.Data;

[Flags]
public enum StuffDataFlags : ushort
{
    None = 0x0000,
    Unique = 0x0100,
}
