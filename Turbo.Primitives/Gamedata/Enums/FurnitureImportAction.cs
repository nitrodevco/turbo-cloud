namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What an import does to one of Habbo's items.</summary>
public enum FurnitureImportAction
{
    /// <summary>The hotel has no definition of it: one is made.</summary>
    Add = 0,

    /// <summary>The definition takes Habbo's new values where the hotel kept Habbo's old ones.</summary>
    Update = 1,

    /// <summary>
    /// Habbo changed only fields the hotel changed itself: the definition keeps the hotel's
    /// values, and nothing is written to it.
    /// </summary>
    Keep = 2,
}
