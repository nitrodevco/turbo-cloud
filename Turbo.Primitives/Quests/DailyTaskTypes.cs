namespace Turbo.Primitives.Quests;

/// <summary>
/// The kinds of daily task the server can count, sent as a task's <c>questTypeCode</c>. The
/// client keeps the code but never reads it (AS3 <c>DailyTaskInfo</c>), and no official capture
/// shows Habbo's codes, so these are the server's own.
/// </summary>
public static class DailyTaskTypes
{
    /// <summary>
    /// Visit rooms other players own; each room counts once. Habbo's "Backpackers Delight"
    /// (<c>quests.daily.EXPLORE.hint</c>: "explore 10 different rooms").
    /// </summary>
    public const string EXPLORE = "explore";

    /// <summary>
    /// Double-click a furni whose definition name is in the task's target list. Habbo's find
    /// tasks (<c>quests.daily.FINDBBQ.hint</c>: "find the BBQ and double-click on it").
    /// </summary>
    public const string FIND_FURNI = "find_furni";
}
