using System;

namespace Turbo.Primitives.Rooms.Furniture.StuffData;

[Flags]
public enum StuffDataFlags : ushort
{
    None = 0x0000,
    Unique = 0x0100,
}
