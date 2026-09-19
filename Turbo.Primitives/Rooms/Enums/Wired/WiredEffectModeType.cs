namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredEffectModeType
{
    All = 0,
    Random = 1,
    FirstOnly = 2,

    /// <summary>Actions that have not run yet go first; once all ran the cycle restarts.</summary>
    Unseen = 3,
}
