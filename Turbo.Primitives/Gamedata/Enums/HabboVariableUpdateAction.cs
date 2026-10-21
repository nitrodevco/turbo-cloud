namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What taking one of Habbo's external variables in does to the hotel's.</summary>
public enum HabboVariableUpdateAction
{
    /// <summary>The hotel has no such variable: it gets Habbo's.</summary>
    Add = 0,

    /// <summary>The hotel's value becomes Habbo's.</summary>
    Update = 1,

    /// <summary>Habbo no longer has it: the hotel's goes.</summary>
    Remove = 2,
}
