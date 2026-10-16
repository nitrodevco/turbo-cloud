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
    /// How often the floor states wired changed go out, together in one message. Habbo sends them
    /// once per wired tick (official client capture, WIN63-202610131318: a 20 Hz timer gave an
    /// ObjectsDataUpdate every ~50 ms, and a click's one ~100 ms after it). A room running a
    /// 0.5 s timer shows one every 500 ms either way.
    /// </summary>
    public int StateUpdateMs { get; init; } = 50;

    /// <summary>
    /// How often signals and stack calls may run the same stack in one window of
    /// <see cref="StackVisitWindowMs"/>; the visit past this is dropped without an error. 100 per
    /// receiving stack every 500 ms in Habbo (official client captures: rings of 2, 12 and 24
    /// stacks stopped at 200, 1200 and 2400 visits; two receivers sent 100 each both completed;
    /// two sends of 100 to one receiver completed 100, or 200 with the second delayed by 500 ms;
    /// a timer feeding a two-stack ping-pong at 2 to 20 Hz ran the whole chain once per ~500 ms
    /// and only flashed the antenna in between, WIN63-202610131318). 0 or less is no limit.
    /// </summary>
    public int MaxStackVisitsPerWindow { get; init; } = 100;
    public int StackVisitWindowMs { get; init; } = 500;

    /// <summary>
    /// How long, in wall-clock milliseconds, one execution's signals and stack calls may keep
    /// running before it is stopped and RECURSION_TIMEOUT is counted. Habbo does not disclose its
    /// duration ("an execution taking too long is stopped", in-client error help). 0 or less is no
    /// limit.
    /// </summary>
    public int RecursionTimeoutMs { get; init; } = 1000;

    /// <summary>
    /// Delayed executions a room may hold at once; one more is dropped and DELAYED_EVENTS_CAP is
    /// counted. Habbo bounds them without saying where (in-client error help; 800 at once worked
    /// in an official client capture). 0 or less is no limit.
    /// </summary>
    public int MaxDelayedEvents { get; init; } = 5000;

    /// <summary>Delayed executions run per tick; the rest wait for the next one.</summary>
    public int MaxScheduledPerTick { get; init; } = 64;
    public int MaxEventsPerTick { get; init; } = 64;

    /// <summary>
    /// Events a room keeps waiting for its wired ticks; past this, new ones are dropped and the
    /// drop is logged. A room that raises more than <see cref="MaxEventsPerTick"/> each tick
    /// would otherwise queue without end.
    /// </summary>
    public int MaxQueuedEvents { get; init; } = 1024;

    /// <summary>
    /// The room's wired usage budget, the monitor's "usage" limit. Usage is what recent executions
    /// cost, decaying over time; an execution that takes it past this is cancelled and
    /// EXECUTION_CAP is counted ("an execution is cancelled when it would exceed the usage cap",
    /// in-client error help). Habbo sizes it per room by a formula it does not disclose, and the
    /// permanent variable caps below scale with it (official client captures of WiredRoomStats):
    /// 4687.5 with 23 / 93 / 70 variables for an owner of level 13, 2500 with 12 / 50 / 37 in
    /// another room (WIN63-202610131318), a factor of about 1.875 throughout. The defaults are the
    /// first room's, so cap and variable limits come from one room. 0 or less is no cap.
    /// </summary>
    public double ExecutionCostCap { get; init; } = 4687.5;

    // What an execution adds to the usage: each stack it runs, each action, and each furni or
    // user an action works on (a signal's antennas and a call's stacks are not targets). Habbo's
    // units are not disclosed; these defaults fit official client captures (WIN63-202610131318):
    // one click on a two-stack signal ping-pong (201 stack runs, 401 actions, no targets) read
    // 170.9, and a timer with one global variable change read 1.1 at 2 Hz and peaks of about 6.25
    // and 11.3 at 10 and 20 Hz. Each target adds about 0.3 (an older capture: a 10 Hz timer
    // changing a variable on 1 furni read about 13, on 100 furni about 445).
    public double ExecutionCostPerStack { get; init; } = 0.05;
    public double ExecutionCostPerAction { get; init; } = 0.4;
    public double ExecutionCostPerTarget { get; init; } = 0.3;

    /// <summary>
    /// Usage falls by <see cref="ExecutionCostDecayFactor"/> every this many milliseconds: x0.6
    /// every 500 ms in Habbo (official client captures, WIN63-202610131318: 170.9, 102.5, 61.5,
    /// 36.9 after a click, the same after each timer was removed; older WIN63-202609091217
    /// traces fitted 11/16).
    /// </summary>
    public int ExecutionCostDecayMs { get; init; } = 500;
    public double ExecutionCostDecayFactor { get; init; } = 0.6;

    /// <summary>
    /// A room whose wired took more than <see cref="HeavyBusyMsPerWindow"/> milliseconds of
    /// server time in one window of <see cref="HeavyWindowMs"/> is marked heavy: a warning in the
    /// monitor (MARKED_AS_HEAVY), not a stop, and apart from the usage cap (an official client
    /// capture logged EXECUTION_CAP while the room stayed not heavy). Habbo does not disclose its
    /// threshold. 0 or less never marks a room heavy.
    /// </summary>
    public double HeavyBusyMsPerWindow { get; init; } = 200;
    public int HeavyWindowMs { get; init; } = 1000;

    /// <summary>
    /// One execution running longer than this, in wall-clock milliseconds, halts all wired in the
    /// room for <see cref="KilledDurationMs"/> and counts KILLED. Habbo halts a room's wired for
    /// 10 minutes on criteria it keeps secret (in-client error help), so this is off (0) unless an
    /// operator sets it.
    /// </summary>
    public int KillExecutionMs { get; init; } = 0;
    public int KilledDurationMs { get; init; } = 600_000;

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
    /// Permanent variable definitions a room may hold per kind. Habbo sizes these per room with
    /// its usage cap (<see cref="ExecutionCostCap"/>): official client captures show 23 furni / 93
    /// user / 70 global with a cap of 4687.5, and 12 / 50 / 37 with 2500.
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

    /// <summary>
    /// Whether "Place Temporary Furni" may copy only Builders Club furni, as on Habbo: its save is
    /// refused with <c>wiredfurni.error.furni_can_not_be_cloned</c> ("You can only select Builders
    /// Club furniture that do not have advanced functionality.") for anything else. Off lets a
    /// hotel without a Builders Club copy any furni; furni that turns into something of value
    /// (credits, presents, pets) is refused either way.
    /// </summary>
    public bool PlaceFurniBuildersClubOnly { get; init; } = true;

    /// <summary>
    /// How long a player told that Place Temporary Furni could not place a copy is not told again;
    /// Habbo's is about 5 s (recorded: none 4.4 s after the last, one 6.6 s after). 0 tells them
    /// on every run that fails.
    /// </summary>
    public int PlacementFailureNotificationCooldownMs { get; init; } = 5000;

    /// <summary>
    /// Whether a player entering a room whose wired writes <c>$(trigger_text)</c> is told that the
    /// room can use the full text of what they say, as Habbo does (official client captures,
    /// WIN63-202610121709 and 202610131318: on every entry while a Show Message text held it, on
    /// none once it was saved without).
    /// </summary>
    public bool NotifyTriggerTextCaptureOnEntry { get; init; } = true;

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
    /// Teleport To without "fast teleportation" (official client capture 2026-10-16, walk-on +
    /// Teleport To, 2 of 2 runs): the user gets this effect when the box fires, stands on the
    /// target <see cref="TeleportDelayMs"/> later and loses the effect
    /// <see cref="TeleportEffectMs"/> after the box fired.
    /// </summary>
    public int TeleportEffectId { get; init; } = 4;

    public int TeleportDelayMs { get; init; } = 2000;

    public int TeleportEffectMs { get; init; } = 3500;

    /// <summary>
    /// How many tiles away Chase notices a player; 0 is the whole room. Habbo chased from 7, 6,
    /// 5, 4, 3 and 2 tiles in an 8-wide room (official client capture 2026-10-16), so no limit
    /// showed there; ours stopped at 3. Farther than 7 is untested.
    /// </summary>
    public int ChaseRange { get; init; } = 0;

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
