namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredVariableGroupSubBandType : byte
{
    /// <summary>Smart variables, which the official client lists above the internal ones.</summary>
    Smart = 0xF0,
    Base = 0xE0,
    Position = 0xD0,
    Meta = 0xC0,
    Other = 0x80,
}
