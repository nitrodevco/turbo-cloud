namespace Turbo.Primitives.Movements.Enums;

/// <summary>What kind of thing a movement moved. Stored by value: never renumber a member.</summary>
public enum AssetKind : byte
{
    Furni = 0,

    /// <summary>A Builders Club furni: placed and picked up, never owned.</summary>
    BorrowedFurni = 1,
    Pet = 2,
    Bot = 3,
    Currency = 4,
}
