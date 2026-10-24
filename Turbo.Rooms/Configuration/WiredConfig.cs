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
    /// Whether "Call Stacks" skips a stack that already ran in the same execution, as Habbo does
    /// (official client capture 2026-10-24, WIN63-202610201032: two stacks calling each other ran
    /// once each per click, 2/2, with no error; a stack calling its own trigger ran once). Off lets
    /// a call run such a stack again, up to <see cref="MaxStackVisitsPerWindow"/>.
    /// </summary>
    public bool CallStacksOncePerExecution { get; init; } = true;

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
    /// in-client error help). Habbo sizes it by the room owner's account level when the room
    /// loads: max(2500, 312.5 x (level + 2)) fits every official client capture of WiredRoomStats
    /// (2500 for levels 3 and 4, 2812.5 for 7, 4687.5 for 13; WIN63-202610131318 and
    /// WIN63-202610201032), and a room loaded before its owner levelled up keeps its old cap
    /// until it unloads. This is the least cap, and every room's when
    /// <see cref="ExecutionCostCapPerOwnerLevel"/> is 0. 0 or less is no cap. The default keeps
    /// every room at the level 13 cap, as before: this hotel's account levels are all the default
    /// one, so Habbo's sizing (2500 and 312.5) would shrink every room. Set those two once
    /// account levels are earned.
    /// </summary>
    public double ExecutionCostCap { get; init; } = 4687.5;

    /// <summary>
    /// What each of the owner's account levels (plus <see cref="ExecutionCostCapOwnerLevelOffset"/>)
    /// is worth in usage cap, when that comes to more than <see cref="ExecutionCostCap"/>; 0 (the
    /// default) is one cap for every room. Habbo's: 312.5.
    /// </summary>
    public double ExecutionCostCapPerOwnerLevel { get; init; }
    public int ExecutionCostCapOwnerLevelOffset { get; init; } = 2;

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
    /// Permanent variable definitions a room may hold per kind, per 10000 of its usage cap
    /// (<see cref="ExecutionCostCap"/> for its owner's level), rounded down: official client
    /// captures of WiredRoomStats show 12 furni / 50 user / 37 global at 2500, 14 / 56 / 42 at
    /// 2812.5 and 23 / 93 / 70 at 4687.5. 0 or less is no cap for that kind.
    /// </summary>
    public int PermanentFurniVariablesPer10000Cap { get; init; } = 50;
    public int PermanentUserVariablesPer10000Cap { get; init; } = 200;
    public int PermanentGlobalVariablesPer10000Cap { get; init; } = 150;

    /// <summary>
    /// Whether only a variable box's owner and the room's owner may save it; anyone else with
    /// wired rights is refused with <c>wiredfurni.error.variables.permissions.2</c> ("Only the
    /// owner of this wired variable or room owner has the permission to make this change.").
    /// </summary>
    public bool VariableBoxOwnersOnly { get; init; } = true;

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
    /// Whether Set Highlight's steady Color tint may only colour Builders Club or temporary furni,
    /// as on Habbo ("Steady Color Tint only works on BC or temporary furniture. Normal furniture
    /// cannot use this setting.", <c>wiredfurni.error.set_furni_highlight.normal_furni</c>): a
    /// save that picks other furni is refused and other furni a selector finds are left out. Off
    /// lets any furni be tinted.
    /// </summary>
    public bool FurniHighlightSteadyTintBuildersClubOnly { get; init; } = true;

    /// <summary>
    /// The strengths Set Highlight's Darken may be saved with; keep it equal to the client variable
    /// <c>wired.furni_highlight.darken_levels</c>, which the editor offers. Habbo's is <c>0</c>
    /// (official external variables 2026-10-20), which hides Darken and refuses any strength above 0
    /// with <c>wiredfurni.error.set_furni_highlight.invalid_strength</c>.
    /// </summary>
    public int[] FurniHighlightDarkenLevels { get; init; } = [0];

    /// <summary>
    /// How long a player told that Place Temporary Furni could not place a copy is not told again;
    /// Habbo's is about 5 s (recorded: none 4.4 s after the last, one 6.6 s after). 0 tells them
    /// on every run that fails.
    /// </summary>
    public int PlacementFailureNotificationCooldownMs { get; init; } = 5000;

    /// <summary>
    /// Who gets the <c>wired.error</c> bubble when a room hits a wired limit Habbo has a text for
    /// (<c>room.error.wired.execution_cap</c>, <c>delayed_events_cap</c>, <c>executor_overload</c>,
    /// <c>marked_as_heavy</c>, <c>too_many_variables</c>, <c>transaction_failure</c>; official texts,
    /// <c>notification.wired.error={"display":"BUBBLE"}</c> official external variables). Habbo
    /// told the room's owner of the usage cap (official client capture 2026-10-20, the owner alone
    /// in the room, timer-driven load); whether others in the room are told too is not known.
    /// </summary>
    public WiredErrorNoticeRecipients ErrorNoticeRecipients { get; init; } =
        WiredErrorNoticeRecipients.Owner;

    /// <summary>
    /// How long one room does not repeat the bubble for the same error. Habbo repeated the usage
    /// cap's every 20.0 s while the room stayed over the cap (official client capture 2026-10-20,
    /// 5 of 5 intervals), and counted EXECUTION_CAP once per bubble; the other errors' interval is
    /// inferred from it.
    /// </summary>
    public int ErrorNoticeCooldownMs { get; init; } = 20000;

    /// <summary>
    /// The bubble's image; empty lets the client pick its default for the type. Habbo's carries
    /// <c>album1584/WIRD2.png</c> (usage cap and placement failure, recorded).
    /// </summary>
    public string ErrorNoticeImage { get; init; } = "${image.library.url}album1584/WIRD2.png";

    /// <summary>
    /// Whether a player entering a room whose wired writes <c>$(trigger_text)</c> is told that the
    /// room can use the full text of what they say, as Habbo does (official client captures,
    /// WIN63-202610121709 and 202610131318: on every entry while a Show Message text held it, on
    /// none once it was saved without).
    /// </summary>
    public bool NotifyTriggerTextCaptureOnEntry { get; init; } = true;

    /// <summary>
    /// Whether Says Keyword reads whispers, unless the whisperer ticked "Disable whispering to
    /// Wired" (<c>memenu.settings.wired_whisper_read_disable</c>, AccountPreferences
    /// wiredWhisperDisabled). Habbo's does (official client capture 2026-10-20: 5 of 5 whispers
    /// fired, to an absent name and to oneself alike; 0 of 2 with the box ticked, chat still firing).
    /// </summary>
    public bool ReadWhispers { get; init; } = true;

    /// <summary>
    /// Whether a player whose whisper fired a Says Keyword box is told so
    /// (<c>room.error.wired.whisper_read</c>, "Wired detected your whisper message"). Off as Habbo
    /// sent none to a room owner whispering (official client capture 2026-10-20, 0 of 7); whether a
    /// visitor is told is not known.
    /// </summary>
    public bool NotifyWhisperRead { get; init; }

    /// <summary>
    /// Whether a Says Keyword box with "Hide the triggerer's message" hides a line only when its
    /// stack's conditions pass, as Habbo does (official client video, nitro-tester 2026-10-22: a
    /// "match all text" box with hide and "triggering user is on furni" showed the line to the
    /// room while the speaker stood beside the furni, and whispered it back to the speaker alone
    /// while they stood on it). Off, the box hides every line it picks up, whatever its
    /// conditions decide.
    /// </summary>
    public bool HideKeywordOnlyWhenConditionsPass { get; init; } = true;

    /// <summary>
    /// Whether wired boxes may pick wall furni at all. On, each box follows Habbo: only the boxes
    /// that work on any furni's state, position, type or variables may (the editor's
    /// <c>allowWallFurni</c>, per box); off, none may.
    /// </summary>
    public bool AllowWallFurni { get; init; } = true;

    /// <summary>
    /// Whether a save that picks a temporary furni (placed by wired) is refused with Habbo's
    /// <c>wiredfurni.error.temp_furni_not_allowed</c> ("Can not select temporary furni in Wired.").
    /// Temporary ids are handed out again, so a stored pick could later name another furni.
    /// That every box refuses them is inferred from the text. Off lets them be picked.
    /// </summary>
    public bool RefuseTemporaryFurniPicks { get; init; } = true;

    /// <summary>
    /// Whether a save naming a furni or user source its slot does not offer is refused with
    /// Habbo's <c>wiredfurni.error.invalid_furni_source</c> / <c>invalid_user_source</c>. Off keeps
    /// the offered sources of the slot and falls back to its default when none is left.
    /// </summary>
    public bool RefuseInvalidSources { get; init; } = true;

    /// <summary>
    /// How far the root tile of the "in neighbourhood" selectors may sit from the centre of their
    /// drawing, each way (Habbo's editor inputs and server: -64 to 64; a further one is clamped).
    /// </summary>
    public int NeighborhoodMaxRootOffset { get; init; } = 64;

    /// <summary>
    /// The furni logics Send Signal and Receive Signal take as antennas
    /// (<c>wiredfurni.error.require_antenna_furni</c>, "Can only select antenna furni."); Habbo's
    /// antennas are <c>wf_antenna1</c> and <c>wf_antenna2</c> (official furnidata 2026-10-13). A furni matches by its class
    /// name or its logic name. Empty lets any furni be an antenna.
    /// </summary>
    public string[] AntennaLogicNames { get; init; } = ["wf_antenna1", "wf_antenna2"];
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
    /// The bubble styles Show Message may be saved with: the editor's style list
    /// (<c>NOTIFICATION_STYLES</c>, actiontypes/_-21t.as:22, AS3 WIN63-202610201032). Any other
    /// style is refused, so a room cannot speak in a bubble its users could not pick. Empty
    /// takes any style.
    /// </summary>
    public int[] ShowMessageStyleIds { get; init; } =
    [
        34,
        200,
        201,
        202,
        210,
        211,
        212,
        220,
        221,
        222,
        223,
        224,
        225,
        226,
        227,
        228,
        229,
        250,
        251,
        252,
    ];

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
    /// The same with "fast teleportation" (official client capture 2026-10-16, 2 of 2 runs):
    /// effect 235, the walk ends on the tile being stepped onto, landing +1.0 s, effect off +1.5 s.
    /// </summary>
    public int TeleportFastEffectId { get; init; } = 235;

    public int TeleportFastDelayMs { get; init; } = 1000;

    public int TeleportFastEffectMs { get; init; } = 1500;

    /// <summary>
    /// How many tiles away Chase notices a player; 0 is the whole room. Habbo steps toward a
    /// player 2 or 3 tiles away in the furni's row or column every time (16/16), and takes a
    /// random straight step when the player is 4 or 5 tiles away (23 firings) or off its row and
    /// column (31 firings) (official client capture 2026-10-24, W1 lab action.11.R1-R3).
    /// </summary>
    public int ChaseRange { get; init; } = 3;

    /// <summary>
    /// How many tiles away Flee notices a player; 0 is the whole room. Habbo stepped away from a
    /// player 2 tiles away in its row or column 8/8 and ignored one 4 tiles away (official client
    /// capture 2026-10-24, W1 lab action.12.R1-R2); 3, as Chase, is inferred.
    /// </summary>
    public int FleeRange { get; init; } = 3;

    /// <summary>
    /// Whether Chase and Flee notice only a player on the furni's row or column (Habbo: a player
    /// off them is never chased or fled, official client capture 2026-10-24, W1 lab
    /// action.11.R3, action.12.R2). Off, they notice the nearest player in range anywhere.
    /// </summary>
    public bool ChaseFleeInLineOnly { get; init; } = true;

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

    /// <summary>
    /// Variable fx addons a room may hold; each one is drawn over every holder of its variable.
    /// A bound of ours on the room's work, not Habbo's: Habbo limits fx per variable box
    /// (<see cref="VariableFxMaxPerVariable"/>) and drew 7 over 3 boxes in one room.
    /// </summary>
    public int VariableFxMaxBoxes { get; init; } = 25;

    /// <summary>
    /// Variable fx one variable box shows. Habbo's is 5 (official client capture 2026-10-20, 2 of
    /// 2): a 6th fx on the same variable box saves and its config is sent, but it is never drawn
    /// (no status), the owner gets a <c>wired.error</c> bubble
    /// (<c>room.error.wired.variable_fx_limit</c>) and VARIABLE_FX_LIMIT counts once; fx on other
    /// variable boxes still draw. 0 or less shows them all.
    /// </summary>
    public int VariableFxMaxPerVariable { get; init; } = 5;

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

    /// <summary>
    /// WIRED Links networks one player may own; creating one more is refused with
    /// <c>wiredlink.error.8</c> ("You have reached the network limit for your account"). Habbo's
    /// is above 264 (official client capture 2026-10-21: 264 created, none refused) and not
    /// disclosed, so this default is ours. 0 turns the cap off.
    /// </summary>
    public int LinkNetworkLimit { get; init; } = 500;

    /// <summary>
    /// Longest message, in UTF-8 bytes, a Send Link Message box stores and sends (AS3
    /// <c>SendLinkMessage.validate</c>: <c>wiredfurni.params.link.message_too_large</c>, "the
    /// 1,000-byte text limit").
    /// </summary>
    public int LinkMessageMaxBytes { get; init; } = 1000;

    /// <summary>
    /// Whether a message or dataframe sent through a network also reaches its Links in rooms that
    /// are not loaded, loading those rooms for it. Off: only loaded rooms receive, as a room with
    /// nobody in it runs no wired, as Habbo's (official client capture 2026-10-21: a send to an
    /// empty room is dropped, not delivered when someone enters later).
    /// </summary>
    public bool LinkDeliversToUnloadedRooms { get; init; }
}

/// <summary>Who is told when a room hits a wired limit (<see cref="WiredConfig.ErrorNoticeRecipients"/>).</summary>
public enum WiredErrorNoticeRecipients
{
    None,
    Owner,
    Room,
}
