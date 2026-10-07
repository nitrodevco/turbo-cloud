namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What made a set of gamedata changes.</summary>
public enum GamedataChangeKind
{
    /// <summary>Habbo's update taken in from one of its releases.</summary>
    Import = 0,

    /// <summary>Staff changed a definition in the panel.</summary>
    Edit = 1,

    /// <summary>An earlier set of changes undone.</summary>
    Rollback = 2,

    /// <summary>Habbo's values put back over the hotel's, for the fields staff chose.</summary>
    HabboValues = 3,
}
