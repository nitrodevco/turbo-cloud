namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// The kinds of thing <c>PlayerPerformsActionEvent</c> reports, in the room's own words. What
/// exactly was done is the event's value: an <see cref="AvatarExpressionType"/>, an
/// <see cref="AvatarDanceType"/>, a sign id or an <see cref="AvatarPostureType"/>. A listener
/// with its own numbering (the wired editor's action list) translates on its side.
/// </summary>
public enum AvatarActionType
{
    Expression = 0,
    Dance = 1,
    Sign = 2,
    Posture = 3,
}
