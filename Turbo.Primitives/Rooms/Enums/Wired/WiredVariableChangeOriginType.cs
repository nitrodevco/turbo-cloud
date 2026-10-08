namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Where a variable change came from, as "Variable Changed" filters it ("Allow triggering from",
/// <c>wiredfurni.params.variables.trigger_origin.0</c> to <c>.3</c>); the trigger's mask bit for an
/// origin is <c>1 &lt;&lt; origin</c>.
/// </summary>
public enum WiredVariableChangeOriginType
{
    /// <summary>Wired in this room.</summary>
    ThisRoom = 0,

    /// <summary>A shared variable changed by another room.</summary>
    AnotherRoom = 1,

    /// <summary>The wired menu's inspection tool (:inspect).</summary>
    Inspection = 2,

    /// <summary>Variable Management or the Variable Web API.</summary>
    External = 3,
}
