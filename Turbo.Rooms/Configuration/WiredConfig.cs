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
    public int MaxErrorLogEntries { get; init; } = 50;

    public int MaxFloorItems { get; init; } = 200;
    public int MaxWallItems { get; init; } = 50;
    public int MaxPermanentFurniVariables { get; init; } = 50;
    public int MaxPermanentUserVariables { get; init; } = 50;
    public int MaxPermanentGlobalVariables { get; init; } = 50;

    // The bounds a saved box is checked against.
    public int SelectorMaxAreaSize { get; init; } = 100;
    public int SelectedItemsLimit { get; init; } = 20;
    public bool AllowWallFurni { get; init; } = true;
    public int MaxIntParams { get; init; } = 16;
    public int MaxCoordinate { get; init; } = 255;
    public int StringParamMaxLength { get; init; } = 2000;
    public int MaxHandItemId { get; init; } = 10000;
    public int MaxEffectId { get; init; } = 10000;
    public int ShowMessageMaxLength { get; init; } = 200;

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
    /// How long a player, avatar or furni is in the room before fx are sent about it. The client
    /// drops an fx for an object it has not been told of yet, and the object travels another way.
    /// </summary>
    public int VariableFxEntryDelayMs { get; init; } = 1000;

    /// <summary>
    /// The effect a wired freeze paints, by the index the box editor sends. Ships as zeros (no
    /// effect): which effect ids a hotel has is hotel data.
    /// </summary>
    public int[] FreezeEffectIds { get; init; } = [0, 0, 0, 0, 0];
}
