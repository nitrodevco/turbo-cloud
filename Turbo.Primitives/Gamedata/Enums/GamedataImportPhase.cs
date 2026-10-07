namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Where taking in a Habbo release is.</summary>
public enum GamedataImportPhase
{
    /// <summary>Reading the furniture asset files it needs.</summary>
    Files = 0,

    /// <summary>Writing the definitions.</summary>
    Import = 1,

    Done = 2,

    Failed = 3,
}
