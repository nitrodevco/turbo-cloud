namespace Turbo.Rooms.Configuration;

/// <summary>
/// Everything an operator can tune about wired, and nothing else: how fast and how deep it
/// runs, how much of it a room may hold, and the bounds a saved box is checked against. A
/// setting belongs here only when wired code reads it; what a game, a bot or a plain furni
/// needs lives in that system's own config, even when wired is its only user today.
/// </summary>
public class WiredConfig
{
    public const string SECTION_NAME = "Turbo:Wired";

    public int TickMs { get; init; } = 50;

    /// <summary>
    /// How often the floor states wired changed go out, together in one message (Habbo: every
    /// 500 ms).
    /// </summary>
    public int StateUpdateMs { get; init; } = 500;

    /// <summary>How deep signals and stack calls may nest before the chain is cut and logged.</summary>
    public int MaxDepth { get; init; } = 20;
    public int MaxScheduledPerTick { get; init; } = 64;
    public int MaxEventsPerTick { get; init; } = 64;

    /// <summary>
    /// Events a room keeps waiting for its wired ticks; past this, new ones are dropped and the
    /// drop is logged. A room that raises more than <see cref="MaxEventsPerTick"/> each tick
    /// would otherwise queue without end.
    /// </summary>
    public int MaxQueuedEvents { get; init; } = 1024;

    /// <summary>Executions in one window that mark a room as heavy in the wired monitor.</summary>
    public int ExecutionCostCap { get; init; } = 500;
    public int ExecutionCostWindowMs { get; init; } = 1000;

    /// <summary>
    /// Lines of "write to logs" a room keeps; the oldest makes room for a new one ("Only the most
    /// recent 200 logs are stored per type", <c>wiredmenu.logs_overview.info</c>).
    /// </summary>
    public int MaxRoomLogEntries { get; init; } = 200;

    /// <summary>
    /// The room's floor and wall furni (all of them, not only wired boxes) the monitor measures,
    /// and past which no more wired boxes are placed: 4000 each in Habbo (official client capture
    /// of WiredRoomStats, WIN63-202610121624).
    /// </summary>
    public int MaxFloorItems { get; init; } = 4000;
    public int MaxWallItems { get; init; } = 4000;

    /// <summary>
    /// Permanent variable definitions a room may hold per kind. Habbo sizes these per room:
    /// official client captures show 23 furni / 93 user / 70 global, and 12 / 50 / 37 in a room
    /// with a lower execution cap.
    /// </summary>
    public int MaxPermanentFurniVariables { get; init; } = 23;
    public int MaxPermanentUserVariables { get; init; } = 93;
    public int MaxPermanentGlobalVariables { get; init; } = 70;

    // The bounds a saved box is checked against.
    // Tiles an area selector may cover; 0 or less is no cap beyond the room's map, which is how
    // Habbo works (recorded on hhus, WIN63-202610121709: 10x18, 12x16 and 50x50 areas saved as sent,
    // and a 10x18 area changes tiles in all 18 rows).
    public int SelectorMaxAreaSize { get; init; } = 0;
    public int SelectedItemsLimit { get; init; } = 20;
    public bool AllowWallFurni { get; init; } = true;
    public int MaxIntParams { get; init; } = 16;
    public int MaxCoordinate { get; init; } = 255;
    public int StringParamMaxLength { get; init; } = 2000;
    public int MaxHandItemId { get; init; } = 10000;
    public int MaxEffectId { get; init; } = 10000;

    /// <summary>
    /// The Show Message editor's limit: 400 since WIN63-202610121624
    /// (<c>TextAreaParam(40, -1, 8, -1, 400)</c>).
    /// </summary>
    public int ShowMessageMaxLength { get; init; } = 400;

    /// <summary>
    /// The longest text a WIRED label keeps: its bubble's input takes 1000 characters
    /// (layout <c>furniture_label</c>, <c>max_chars</c> 1000); longer text is cut there.
    /// </summary>
    public int FurnitureLabelMaxLength { get; init; } = 1000;

    /// <summary>
    /// The longest delay an action may wait, in half-second pulses: 120 (60 s) is what the
    /// WIN63-202610121624 editor offers, standard or precise (<c>ActionDelaySection</c>).
    /// </summary>
    public int ActionDelayMaxPulses { get; init; } = 120;

    /// <summary>
    /// How long a pattern built from player text may take to match a chat line before the
    /// match is given up and logged.
    /// </summary>
    public int RegexMatchTimeoutMs { get; init; } = 50;
    public int KickMessageMaxLength { get; init; } = 100;
    public int LogMessageMaxLength { get; init; } = 400;
    public int MaxRewardsPerBox { get; init; } = 20;

    /// <summary>The most a wired counter shows: 99:59.5, counted in half seconds.</summary>
    public int ClockMaxHalfSeconds { get; init; } = 11999;

    /// <summary>Variable fx addons a room may hold; each one is drawn over every holder of its variable.</summary>
    public int VariableFxMaxBoxes { get; init; } = 25;

    /// <summary>The most fx values one player is shown at once; a variable on every furni stops here.</summary>
    public int VariableFxMaxStatusesPerViewer { get; init; } = 500;

    /// <summary>How often changed fx values go out. Changes in between reach the client as one batch.</summary>
    public int VariableFxFlushMs { get; init; } = 100;

    /// <summary>
    /// How often changed values reach the leaderboards viewers hold open. Changes in between go
    /// out as one update per chunk.
    /// </summary>
    public int VariableLeaderboardFlushMs { get; init; } = 1000;

    /// <summary>
    /// How long a player, avatar or furni is in the room before fx are sent about it. The client
    /// drops an fx for an object it has not been told of yet, and the object travels another way.
    /// </summary>
    public int VariableFxEntryDelayMs { get; init; } = 1000;

    /// <summary>
    /// Longest "says something" keyword that still fires. The editor takes 1000 characters, but
    /// Habbo fires a pattern of 191 and not one of 202 (official client capture); the exact
    /// threshold in between is not known.
    /// </summary>
    public int ChatTriggerPatternMaxLength { get; init; } = 200;

    /// <summary>
    /// Created variables one user or furni may hold at once; a give beyond it is refused and
    /// logged as TOO_MANY_VARIABLES. Habbo's threshold is not disclosed (its help calls it a high
    /// limit), so this default is ours. 0 turns the cap off.
    /// </summary>
    public int MaxVariablesPerHolder { get; init; } = 500;
}
