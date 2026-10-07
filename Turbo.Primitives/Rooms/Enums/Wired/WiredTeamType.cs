namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// The kind of team "Join Team" puts a user in (<c>wiredfurni.params.team_type.0</c> to <c>.2</c>):
/// the team colour is the same, the game whose team effect the user wears differs.
/// </summary>
public enum WiredTeamType
{
    Wired = 0,
    BattleBanzai = 1,
    Freeze = 2,
}
