namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// What "Move user to furni" does to a walk the user was on when it moved them
/// (<c>wiredfurni.params.user_move.walkmode.0</c> to <c>.2</c>).
/// </summary>
public enum WiredUserWalkModeType
{
    /// <summary>Keep walking to where they were going if the move took them closer to it.</summary>
    KeepWalkingIfCloser = 0,

    /// <summary>Keep walking to where they were going.</summary>
    KeepWalking = 1,

    /// <summary>Stop walking.</summary>
    StopWalking = 2,
}
