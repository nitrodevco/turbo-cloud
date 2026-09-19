namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Who is shown a variable fx (the editor's audience dropdown). The client never filters: the
/// server sends a status only to the players this allows.
/// </summary>
public enum VariableFxVisibilityType
{
    /// <summary>Only the player the fx is drawn over. User fx only.</summary>
    OnlyUser = 0,

    /// <summary>That player and their game team. User fx only.</summary>
    GameTeam = 1,
    Everyone = 2,

    /// <summary>Viewers who have the chosen user variable.</summary>
    HasVariable = 3,

    /// <summary>Viewers whose chosen user variable has the chosen value.</summary>
    HasVariableWithValue = 4,
}
