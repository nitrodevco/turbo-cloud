# Chat commands

Design for a command system: a player types `:name args` in room chat and a command runs instead
of the line being said, and the same commands can run from the server console. Core provides the
machinery and the few commands the Habbo clients already expect a server to answer. Everything
else (a roleplay's `:hit`, a casino's `:roll`) belongs in a plugin.

**Status: being built on top of the merged permission system (`docs/permissions.md`).** The first
change ships the engine, `:commands`, `:kick`, `:mute` and the `command_logs` table; the console executor,
the owner commands (`:pickall` and friends) and a client extension for autocomplete follow. Commands complement a hotel's UI rather than replace it, so core keeps its own
commands to the minimum.

## 1. Evidence

The clients show what players and servers already expect. Older emulators show what plugin
commands need, and what went wrong, not how to build it.

### Turbo today

Every room line goes through `RoomChatSystem.SendChatFromPlayerAsync`:

1. truncate;
2. word filter;
3. mute checks (room, hotel through `chat.speak`, flood);
4. flood check;
5. chat style;
6. raise `PlayerChatEvent`, which handlers may rewrite or cancel;
7. broadcast, pet commands, chat log, and the room event wired's "says keyword" trigger reads.

There is no command system. A plugin can cancel the event and treat the line as a command, but
then every plugin parses `:` its own way, two plugins can claim the same word, nothing lists what a
player may use, and each checks permissions by hand.

### The clients

AS3 (`ChatInputWidgetHandler`) is the behavioral source of truth. The official JavaScript
client provides implementation guidance; nitro-next handles input in `RoomChatInputView` and
`wiredChatCommands.ts`:

| What the client does with a `:word` line | AIR / Flash | nitro-next |
| --- | --- | --- |
| Runs it itself; the server never sees it | about 50: `:shake`, `:sign`, `:chooser`, `:furni`, `:zoom`, `:visit`, `:roomid`, `:ignore`, `:floor`, `:fps`, `:screenshot`, `:wired`, `:lang`, `o/`, and `:kick` / `:mute` below security level 4 (sent as the kick and mute packets instead) | only its wired commands: `:wf`, `:wired`, `:var`, `:inspect`, `:playtest`, `:wiredreset` |
| Sends it to the server as chat **on purpose** | `:pickall`, `:pickallbc`, `:resetscores`, `:ejectall`, `:ejectpets`, `:moonwalk`, `:habnam`, `:yyxxabxa`, `:mutepets`, `:mpgame`, and `:kick` / `:mute` from security level 4 | everything not in the row above |
| Any other `:word` | sent as ordinary chat | sent as ordinary chat |
| A whisper | never a command | never a command |

AIR replaces a second word `x` with the name of the selected avatar, so `:kick x` arrives as
`:kick Alice`. Turbo answers none of the second row today: a moderator's `:kick Alice` is said out
loud.

### Plugin requirements

Core owns argument validation, target lookup, permission checks and localized usage replies.
Plugins own game-specific rules such as cooldowns, distance limits and character state, and can
veto execution through the command events. Command registration must reject ambiguous names.

## 2. Decisions

Each question was put to Jev (TypeSafe) with the evidence above. The instruction was to design for
correctness and plugin authors, treating the older emulators as evidence rather than a model to
copy. The questions were asked three times as evidence was added: the clients and Arcturus, then
FuseRP and Skylight3, then the whole survey. The rows under *Settled* held across those runs. Four
moved each time the evidence grew and ended near an even split; they were decided by the focused
pass below.

### Settled

| # | Question | Decision | Jev |
| --- | --- | --- | --- |
| 1 | Match before or after the word filter? | **Before**, on the unfiltered text, so a filtered word in a name or reason is not mangled. A line that is not a command is filtered as today. | 0.78 |
| 2 | Can a whisper run a command? | **Never.** Neither client treats a whisper as one. | 0.99 |
| 3 | Can a muted player run commands? | **No**: every mute check runs first. | 0.97 |
| 6 | Two registrations claim the same name or alias? | **The second fails its plugin's load** with nothing registered, as the permission registry does. Plugins register in parallel, so "first wins" would depend on timing. | 0.99 |
| 7 | A name some client swallows (`:sign`)? | **Allowed, with a warning** naming the clients that cannot reach it; the swallowed set differs by client and version. | 0.77 |
| 10 | A command only for rooms the player controls? | **Declared**: a minimum room controller level that core checks, alongside any node. | 0.88 |
| 11 | Log command use? | **When the executor holds `command.log`**, so an operator logs staff and nobody else. | 0.78 |
| 12 | Who can run a command? | **An executor**, a permission subject: a player in a room, the console, later RCON. A command declares what it needs, and core refuses it where that is missing. | 0.97 |
| 13 | Per-command cooldowns in core? | **No**: a game rule, the command's own. | 0.86 |
| 14 | A command throws? | **Contained**: logged with the command and executor, a generic failure reply, the line not said, the room carries on. | 0.98 |
| 15 | Build now, while permissions wait? | **Built after permissions merged.** | 0.79 |
| 16 | Room owners disabling commands per room? | **Not in core**: a plugin can veto in a room through the pre-execution event. | 0.89 |
| 17 | How does a plugin stop a command from its own game state (dead, cuffed)? | **A cancellable pre-execution event carrying the command's descriptor**, so a plugin reads its own attributes on its commands (`[UsableWhileDead]`) and vetoes. Core defines no game-state flags. | 0.98 |
| 18 | How do commands report back? | **A result, turned into a whisper from hotel texts keys by core**: binding errors, target not found, no room, and the command's own status, so a hotel can translate and reword every reply. | 1.00 |

### Decided in a focused pass

Four questions stayed near an even split in the broad runs, so each was asked again the way
TypeSafe recommends:
- **focused state:** only that decision's evidence, and the consequence of each option spelled out;
- **scored separately:** each option on correctness and safety, players, plugin authors, operators,
  build cost, and whether it can be changed later without breaking anything;
- **checked in code:** the scores were combined under set weights and under equal weights, and
  compared with Jev's direct pick.

Two came out clear at once. For the other two, the direct pick and the scores disagreed. So the
questions each one hinged on were asked on their own, with the facts that settle them (Turbo's
real flood limits, HabboRP-br's command names), and the choice was run three times to check it was
stable.

| # | Question | Decision | Jev |
| --- | --- | --- | --- |
| 4 | Commands and flood control | **One shared flood window, but a command never mutes chat.** Commands and chat count against the existing window and counter, with no new setting. A command over the limit is dropped with the usual flood reply; only a chat line over the limit starts the 30-second chat mute. What settled it: at Turbo's limits (4, 6 or 8 lines per 4 seconds, set by the room owner), ordinary command use would trip the mute (0.72–0.75); a chat mute is the wrong answer to fast commands (0.35); and a second, command-only limiter would be a burden (0.74). This option answers all three (0.82). | 0.61–0.64 over 3 runs (counting as chat 0.29–0.33) |
| 5 | A known command the player may not use | **Swallowed, with a short refusal whisper** from hotel texts, so a mistyped or out-of-place staff command is never broadcast to the room. The cost, accepted: a player can learn which command names exist. Unknown names are still said as chat. | 0.74; set and equal weights agree |
| 8 | Operators renaming commands | **Names are declared in code; hotel texts may add aliases** (`command.<name>.aliases`) but never rename or remove the declared name, so every documented name keeps working. An added alias that clashes is reported and ignored when the texts reload. What settled it: HabboRP-br forked core to give core's commands Portuguese names beside the English ones (`pickall`/`pegartudo`, `ban`/`banir`, `dc`/`desconectar`), so the need is real today (0.89) and code-only names would force such a fork (0.89). | 0.72–0.78 over 3 runs |
| 9 | Arguments | **Typed parameters.** Core binds and validates them, resolves names, and replies with generated usage text; the command body never indexes an array. A rest-of-line parameter still gives a command raw text when it wants it. | 0.93; set and equal weights agree |

## 3. Shape

A sketch to review, not an API to hold anyone to.

### Declaring a command

A command is a class in core or a plugin assembly. A feature processor finds it as the plugin
loads and removes it when the plugin unloads, as handlers and permission sources are found:

```csharp
[Command("kick", Description = "Kick a player from the room")]
[RequiresPermission(PermissionNodes.Command.KICK)]
[RequiresRoomLevel(RoomControllerType.Rights)]
public sealed class KickCommand : ICommand<KickCommand.Arguments>
{
    public sealed record Arguments(
        [Description("who to kick")] IRoomPlayer Target,
        [Description("why")] RestOfLine? Reason
    );

    public async ValueTask ExecuteAsync(
        ICommandContext ctx,
        Arguments args,
        CancellationToken ct
    )
    {
        // args.Target is a player in this room; core replied "usage: :kick <who> [why]" if not.
    }
}
```

- **Names and aliases** are declared on the attribute. A hotel's texts may add aliases under
  `command.<name>.aliases`, never rename or remove the declared name (decision 8).
- **Parameters** are a record the command declares, and core binds the values: an avatar in the
  room, a player anywhere, a number, an enum, the rest of the line, optional values. Core replies
  with usage text built from the record when the arguments do not fit (decision 9). The binder
  answers the lookups FuseRP wrote 39 times by hand, and AIR's `x` has already been replaced by a
  name.
- **Permission.** `[RequiresPermission]` is the attribute the packet gate reads, with any one of
  its nodes enough. A core command's node is `command.<name>`; a plugin's is
  `<prefix>.command.<name>`. A command everyone may use registers its node `GrantedByDefault`, and
  a hotel takes it away with a denial. A command without the attribute is refused at
  registration, so `:commands` can list exactly what an executor holds.
- **Room level.** `[RequiresRoomLevel]` is checked against the executor's controller level where
  they typed it (`room.control.any` counts as `Moderator`) (decision 10). A command with it needs a
  room, so the console cannot run it.
- **The registry** refuses a second registration of a name or alias (decision 6), and warns for a
  name AIR runs itself (decision 7).

### The executor

`ICommandExecutor` is a permission subject with an optional player and room. There are two kinds:
a player typing in a room, and the console, which holds every node (decision 12). A command that
declares it needs a room, or a room player parameter, runs only for an executor in one; core
refuses it elsewhere with a reply. `perm` stays as it is: the console's own commands are not chat
commands.

### Where it runs in the chat pipeline

1. truncate;
2. mute checks (decision 3), then the flood check. A command counts against the flood window, but one over the limit is dropped rather than muting chat (decision 4);
3. **if the line is said or shouted, and its first word is `:` and a registered name the executor
   may use here, bind and run the command on the unfiltered text, and stop** (decisions 1 and 2). A known command the executor may not use here is swallowed with a refusal whisper (decision 5);
4. otherwise as today: filter, style, `PlayerChatEvent`, broadcast and the rest.

A command runs in the room grain's turn, reads room state without a call, and awaits other grains
without blocking. An exception is caught, logged and answered with a whisper (decision 14).

### Replies

A command returns a `CommandResult`: success, or a status with parameters. Core turns a result
into a whisper from the hotel texts under `command.<name>.<status>`. Its own failures (arguments
that do not fit, a target not found, no room) use shared keys (decision 18), so a hotel can reword
or translate every reply without touching code.

### Events and logging

- `CommandExecutingEvent` is cancellable, raised after binding and before the command runs. It
  carries the command's descriptor, its type and attributes, so a plugin can veto on its own
  state: a roleplay puts `[UsableWhileDead]` on its commands and cancels the others for a dead
  player (decision 17). Core knows nothing about being dead.
- `CommandExecutedEvent` is raised after, with the outcome.
- A use by an executor holding `command.log` goes to a `command_log` table: executor, room,
  command, argument text, outcome, time (decision 11).

## 4. What core ships

The commands a Habbo client already sends to the server as chat (§1), and each only once the
feature behind it exists in Turbo. Until then its name is not registered, and the line is said as
chat as it is now. Operator commands are in §4.1.

| Command | Node | Needs | Behind it |
| --- | --- | --- | --- |
| `:commands` | `command.commands` (granted by default) | — | lists what the executor may use |
| `:kick <who>`, `:mute <who> [minutes]` | `command.kick`, `command.mute` | a room | `RoomModerationModule`, which already refuses a target holding `room.moderate.any`. The client sends these as chat only from security level 4, so the nodes sit with `room.moderate.any`. |
| `:pickall`, `:pickallbc`, `:ejectall`, `:ejectpets` | `command.pickall`, ... | owner | room furniture and pets |
| `:resetscores`, `:mutepets` | `command.resetscores`, `command.mutepets` | rights | game scores, pet chat |

`:moonwalk`, `:habnam`, `:yyxxabxa` and `:mpgame` are client easter eggs and a game. Nothing
needs them, so none is planned.

### 4.1 Operator commands (built)

The first table is what the clients already send. Hotel operators also need bans, alerts,
summon and follow, currency and badge grants, user information, online counts, reloads,
maintenance and shutdown. Each command is a thin shell over the service that owns the operation,
so commands are an additional way to use that service rather than its only entry point.

**How they run.** An operator command implements `IOperatorCommand<TArgs>` and runs *outside* the
room's turn, so it may name a player who is offline or in another room and await any grain. The
room checks the node against its own copy and the flood limit, then hands the line to
`IOperatorCommandRunner`, which binds, lets a plugin veto (`CommandExecutingEvent`), runs, replies
(a whisper, through `IRoomGrain.WhisperToPlayerAsync`) and logs. The executor is an
`IOperatorExecutor`: a player in a room, or the server console, which holds every node and runs any
operator command by name (`ban Alice 7d spam`). Its player parameter is a `PlayerTarget`, resolved
by name across the hotel, and a duration is a `CommandDuration` (`30m`, `12h`, `7d`, `2w`, `perm`;
a bare number is refused). The few that need the room's live state (`:roomkickall`, `:roommute`,
`:roomunmute`, `:unloadroom`) stay room commands.

None is granted by default. A migration (`AddPlayerSanctions`) gives each rung of the staff ladder
what suits it; `admin` holds `*`.

| Area | Command | Node | Behind it |
| --- | --- | --- | --- |
| Moderation | `:ban <who> <duration> [reason]`, `:unban <who>` | `command.ban`, `command.unban` | `ISanctionService` and the `player_sanctions` table; checked at login; a lifted ban is stamped, never deleted. Staff holding the node are out of reach. |
| | `:silence <who> <duration>`, `:unsilence <who>` | `command.silence` | A temporary denial of `chat.speak` through the permission grains, so audited already; the player is told. |
| | `:tradelock <who> <duration>`, `:untradelock <who>` | `command.tradelock` | The same, denying `trade`. |
| | `:disconnect <who>` | `command.disconnect` | `ISessionGateway.DisconnectPlayerAsync`. |
| | `:roomkickall`, `:roommute`, `:roomunmute` | `command.roomkickall`, `.roommute`, `.roomunmute` | Room commands. Clearing skips the owner, anyone with `room.moderate.any`, and the executor. |
| Announcements | `:warn <who> <msg>`, `:alert <who> <msg>` | `command.warn`, `command.alert` | `ModeratorMessage` / `HabboBroadcast`. `@room` and `@online` need `command.alert.mass`. |
| | `:roomalert <msg>`, `:hotelalert <msg>` | `command.roomalert`, `command.hotelalert` | The room's players when the line was typed; everyone online. |
| | `:eventalert [msg]` | `command.eventalert` | A hotel notice naming the room, with an `event:navigator/goto/<id>` link. Whether a client follows that link is not verified; the room name and number are in the text. |
| Support | `:whois <who>` | `command.whois` | Online state and room, groups, ban, silence and trade lock, wallet. The player rows hold no address, so none is shown. |
| | `:follow <who>`, `:summon <who>` | `command.follow`, `command.summon` | `ForwardPlayerToRoomAsync`: the room's door still applies. |
| | `:give <who> <currency> <amount>` | `command.give` | Wallet grain; negative takes, never below nothing; currencies are the hotel's `currency_types` names; capped by `MaxCurrencyAmount`. |
| | `:givebadge`, `:takebadge <who> <code>` | `command.givebadge`, `command.takebadge` | Badge grain. |
| | `:giveitem <who> <furni> [count]` | `command.giveitem` | `GrantFurnitureAsync`, furni by definition name; capped by `MaxGiveItemCount`. |
| | `:gift <who> <furni> "<note>" [badge] [trusted]` | `command.gift` | `ReceiveStaffPresentAsync`: a gift wrapped present with no sender on the tag (the client's "Special Gift"), furni by definition name or id; the badge is given as it is opened; `true` drops the client's untrusted-sender warning, which Habbo's own staff gift keeps. Note capped by `MaxGiftMessageLength`. |
| Administration | `:group add\|remove <who> <group> [duration]` | `command.group` and `permissions.manage` | The permission grains and audit; needs both nodes, so the command alone cannot hand out a group. |
| | `:perm check <who> <node>` | `command.perm` | `ExplainAsync`: what decided, and what it beat. |
| | `:status`, `:online` | `command.status`, `command.online` (`command.online.list` for names) | Uptime, players, rooms, silos, memory, availability. |
| | `:maintenance <minutes\|off> [reason]`, `:shutdown [minutes\|cancel] [reason]` | `command.maintenance`, `command.shutdown` | `IHotelAvailability`: reminders as the countdown runs, then maintenance closes login to players without `hotel.maintenance.bypass`, or a shutdown sends everyone home and stops the host. A bare `:shutdown` counts down `DefaultShutdownMinutes`. |
| | `:reload <subject>` | `command.reload` | Subjects: `catalog`, `texts`, `furni`, `navigator`, `currencies`, `chatstyles`, `roommodels`, `petbreeds`, `petspeech`, `achievements`, `plugins`, `filter`. |
| | `:unloadroom` | `command.unloadroom` | Room command: everyone out, the owner too, and the room unloads. |

**Targets.** `:give`, `:givebadge`, `:takebadge`, `:giveitem`, `:alert` and `:warn` accept `@room`
or `@online` rather than having a mass variant each. A command declares which node lets an executor
use them on its target parameter, `[Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who`,
and the binder reads it at registration: on anything but a `PlayerTarget`, or on two parameters, the
command is refused. `ctx.SelectAsync(who, ct)` takes the node from there, and a target without the
attribute takes no selector whoever runs it. Because it is declared, `:commands` tells an executor
who holds the node which parameter may be a group (`:give <who> ... (<who> may be @room or @online)`)
and tells nobody else. A selector use is always logged, whether or not the executor holds
`command.log`.

**Replies.** Statuses are hotel texts as for room commands. The multi-line reports (`:whois`,
`:perm`, `:status`, `:online` list) are English notices and are not translatable yet. The ban and
maintenance messages are hotel texts (`moderation.ban.message`, `hotel.maintenance.started`).
What the help window's sanction info says about a silence or a trade lock is a hotel text too
(`moderation.sanction.mute`, `moderation.sanction.trade_lock`, each with a `.permanent` form).

**Deliberately not here.**
- Avatar toys: `:sit`, `:lay`, `:moonwalk`, `:carry`, `:enable`, `:mimic`, `:push`, `:pull`,
  `:faceless`, `:kiss`, `:hit`. A plugin may add them.
- Per-player toggles (`:disablewhispers`, `:dnd`, `:flagme`) belong in client settings.
- Commands that duplicate a screen: `:setspeed`, `:setmax`, `:convertcredits`, `:bubble`,
  `:namecolour`.
- Game rules: `:pay`, `:sellroom`, `buy*`. A plugin's.
- `:makesay` impersonates a player, and `:override` and `:teleport` are debug toys.
- `:regenmaps` and `:fixtiles` patch broken state; fix the cause.
- `:invisible` needs avatar support. Deferred.

### 4.2 Confirming what reaches the hotel (built)

A line that reaches a lot of the hotel does nothing until its executor confirms it, as Terraform's
`plan` waits for `apply`. The server says what it would do and how to go ahead (`That reaches 312
players. Type :confirm within 30 seconds to go ahead.`), and `:confirm` runs it.

- **What waits.** A selector that names `Turbo:Commands:ConfirmAtPlayers` players or more (10 by
  default), and `:roomalert`, `:hotelalert` and `:eventalert` past the same number, through
  `ctx.ShouldConfirm(players)`. `:shutdown` and `:maintenance` always wait, through
  `ctx.IsConfirmed`. A command asks with `CommandResult.Confirm(status, ...)`: its own text says
  what it would do, and core adds `command.error.confirm_prompt`.
- **`:confirm`.** Granted to everyone (`command.confirm`): it can only go ahead with one's own line.
  It executes the prepared command with its original bound arguments and captured player ids,
  using the confirming executor's current permissions. Command and selector nodes are checked
  again. Players who join afterwards are not added to `@online` or a broadcast. It must be typed in the same room,
  within `Turbo:Commands:ConfirmationSeconds` (30), and it is spent once. A new line to confirm
  replaces the one waiting. The console confirms the same way.
  Unloading and replacing the command invalidates its pending confirmation.
- **Audiences.** Selectors capture their audience automatically. A broadcast uses
  `ctx.SnapshotRecipients(scope, recipients)` before asking to confirm. Each audience has its own
  scope and is retained across confirmation; a confirmed run cannot introduce a new audience.
  Domain eligibility is still checked during execution; a captured id is not a guarantee of delivery.
- **Logged.** The waiting line is logged with the outcome `confirm` and the confirmed run as
  `completed` on success; a returned `CommandResult.Fail` is `failed`, and an exception is `error`.
  Both runners use `CommandResult.Outcome`, independently of the reply text.
- **Not room commands.** `:roomkickall` and `:unloadroom` run in the room's turn and reach one room;
  they do not wait.

### 4.3 Bulk execution and finalisation

`give`, `giveitem`, `givebadge` and `takebadge` use `ctx.ExecuteBatchAsync` over the resolved
players. Core executes at most `Turbo:Commands:MaxBatchConcurrency` players concurrently (4 by
default), with each player's operations in order. Duplicate player ids are executed once.
Each unit is attempted once: a confirmed refusal is counted separately from an exception,
whose commit state is indeterminate. An exception stops that player's remaining units while
other players continue. Cancellation stops new work and retains completed, indeterminate and
unattempted quantities; core does not retry or roll back grants.

`CommandResult.Batch` contains the per-player results. Fully successful batches keep their
existing replies. Incomplete batches use the hotel's `command.error.batch_result` text to report
completed, partial, failed and unattempted players, plus succeeded, refused, indeterminate and
unattempted operations. A batch with some confirmed successes and incomplete work is `partial`,
including when canceled; without successes it is `failed`, `error`, or `canceled` as appropriate.
Confirmation retains the batch result and outcome without sending the reply twice.

Both runners record telemetry and queue or write the audit before publishing completion hooks.
Operator finalisation uses a separate cancellation token for each step, bounded by
`Turbo:Commands:FinalizationTimeoutSeconds` (10 by default). Request cancellation after an
operation completes cannot reclassify it or skip its audit. A failed or timed-out audit is logged
and completion hooks are still attempted; observer failures cannot change the terminal outcome.
A timeout bounds the wait, not the underlying operation: hooks must honour their token. These
bounds apply outside the room's turn; room hooks stay awaited in that turn.

## 5. Open

- **Wired.** A command stops before the room event, so the "says keyword" trigger never sees it.
  Decide when a wired box needs to.
- **Tab completion.** Neither client asks for it.
- **RCON.** It needs an executor kind of its own when Turbo has RCON.

## 6. Build notes

What the build found that the design did not say.

- **A command over the flood limit sends no flood packet.** nitro-next answers
  `FloodControlMessage` by locking the chat input for the given seconds, which is the chat mute
  decision 4 rules out. A command over the limit is dropped with a `command.error.flood`
  whisper and nothing else.
- **`:kick` and `:mute` are granted by default.** nitro-next sends every `:kick` as chat whatever
  the security level, and the room's own rules (`WhoCanKick`, `WhoCanMute`, and the target
  protections in `RoomModerationModule`) already decide who may. A hotel takes the command away
  with a denial. This replaces the "nodes sit with `room.moderate.any`" note in §4.
- **Hotel texts are the client's `ExternalTexts` today**, so every reply key has a default text in
  code and a hotel's text of the same key overrides it. Aliases are read from
  `command.<name>.aliases` and rebuilt when the texts reload.
- **A command runs inside the room grain's turn.** It is handed an in-turn room surface, not a
  grain reference, because a plugin awaiting its own room would deadlock. It must not await a
  presence flow; closing a session is `LogAndForget`, as `KickPlayerBySystemAsync` does.
- **`:commands` is grouped.** A command declares a `Category` on `[Command]`, `General` when it
  names none (core's `:kick` and `:mute` are `Moderation`; a plugin names its own, such as
  `Roleplay`). The list is a scrollable notice, one item per line: a heading per category, `General`
  first and the rest alphabetically, and each command's usage and description alphabetically
  under it. A category is one item, a `---- NAME ----` heading with its commands on the lines
  straight under it, so the window's own padding is the gap between categories. The notice is the client's message-of-the-day window, so it shows only while the
  client's `notification.items.enabled` is on.
- **Matching never allocates.** Names are found with a span lookup, so an ordinary chat line, or a
  `:word` nobody registered, costs no allocation before the line goes on as chat.

## 7. Telemetry

Opt-in, like the room telemetry (`docs/telemetry.md`), and silent when nothing listens.

- Source and meter `Turbo.Commands`; span `command.execute`, tagged `command.name`,
  `command.outcome` and `room.id`.
- Histogram `turbo.command.duration` in seconds, tagged `command` (the registered name, so the
  cardinality is the number of commands) and `outcome`: `completed`, `refused`, `room_level`,
  `bind_failed`, `vetoed`, `flood`, `failed`, `partial`, `confirm`, `error` or `canceled`.
- Never a player id and never argument text, which can be anything a player typed.
- A command that holds the room's turn longer than `Turbo:Rooms:CommandSlowWarningMs` (default 250) is logged as a
  warning with its name and room.

## 8. Command log

A use by an executor holding command.log is written to command_logs: room, player, command
name, the arguments as typed (cut at 255, redacted for sensitive commands), the outcome and the time it ran. A hotel
logs staff and nobody else by giving the node to the groups it wants; the migration gives it to
	rial_moderator, and so to everything that inherits it. A flooded line is not logged.

The row is not written in the room's turn. The room keeps the use in a buffer bounded by
Turbo:Rooms:MaxPendingCommandLogs, hands it to IRoomPersistenceGrain.EnqueueCommandLogsAsync
with its other write buffers, and the persistence grain writes batches of
Turbo:Rooms:MaxCommandLogsPerFlush on the chatlog timer and when it deactivates. A write that
fails goes back on the queue in order, and when the queue is full the oldest uses are dropped with
a warning. The ids are plain columns, not foreign keys: the log outlives the room.

## 9. Client autocomplete (built on the server)

A client completes and checks a line from the command schema the server already has: the
`chat.commands.v2` extension in `docs/client-capabilities.md` sends each player the commands they may
use, with every parameter's type, and answers suggestion requests for values only the server knows
(players, currencies, furni, groups, nodes). The selector node it needs is declared on each
command's `PlayerTarget` parameter (§4.1). nitro-next asks for it and completes in its chat input.


## 10. Grammar, execution and audit guarantees

Argument records can declare literal branches with `[CommandBranch("add", typeof(AddArguments))]`.
Each concrete record derives from the declared base and carries only that branch's arguments.
A branch may require an additional permission. `group add` takes an optional duration; `group
remove` rejects it. `perm check` has its own branch. Simple command declarations are unchanged.

`CommandParameter` supplies help text, integer bounds, string-length limits and sensitive-input
metadata. Integers accept a leading plus or minus; enums accept named members only. Word arguments
also accept double-quoted strings with quote/backslash escapes. `RestOfLine` remains raw text.
Binding failures retain source ranges. Plugins may register a stateless `ICommandArgumentParser`
for their own value types; parser registrations unload with the plugin. Sensitive commands redact
the complete argument text in command audit rows, including malformed input.

`:commands [command]` and its `:help` alias use the execution schema for detailed help. Console
`help [command]` uses the same formatter. Console `perm check` reaches the same operator command
as chat; other existing console permission administration remains available through `perm`.

Known commands longer than the configured chat limit are rejected before truncation. Unknown
colon-prefixed lines and muted-player behavior keep the established chat policy.

Operator requests enter a bounded FIFO per executor on each host. Main and branch permissions and command
registration are checked at execution time. Deadlines are cooperative: a command ignoring
cancellation retains its slot until it settles. The batch concurrency limit is shared
across invocations on that host. Neither partial nor indeterminate operations are automatically retried;
callers must inspect the recorded target results before requesting further mutations.

Pending confirmations are capacity-bounded and periodically expired. Confirmation uses the frozen
audience and checks current permissions and registration. Suggestion windows are also bounded
and periodically expired. Limits and deadlines are in `Turbo:Commands`.

Migration `AddCommandExecutionAudit` adds nullable execution, parent and confirmation IDs, source,
resolved-audience JSON and per-target batch accounting to `command_logs`. Existing rows and room
logs remain valid. Operator audit writes remain best effort after execution: a database failure
is logged, cannot roll back already completed cross-grain operations, and must not trigger retry.
Apply the migration before running this version of the server. No live database migration is
performed by code-generation or test checks.

Client grammar and contextual suggestions use the negotiated `chat.commands.v2` extension;
see `client-capabilities.md` for its exact wire layout. Older clients still use chat commands.

## 11. Executor feedback and target notices

Command results describe the applied action. Target notices are best effort and cannot turn a
completed action into a failure. Executors receive separate warnings when a target is offline
or notice delivery cannot be confirmed. Replies use a room whisper when available and fall back
to a moderator-message dialog after leaving or unloading the room.

Kicks and room clears/unloads retain the native room-kicked notice, without an additional
moderator-message dialog. Targets receive additional notices for room mutes, hotel silence/trading
restrictions and their removal, group changes, currency/item grants, badge removal, unbans and
summon requests. Badge grants retain their existing native receipt notification. Item notices
report the quantity actually granted, including partial batches. Disconnect uses a farewell;
ban retains its native ban message. Inspection commands do not notify their subjects.

Most notices use hotel text keys with built-in defaults and the existing moderator-message packet.
`Turbo:Players:NoticeTimeoutMs` bounds each attempt. Presence checks the active session and queues
the notice in one turn: ordinary offline notices are skipped. A successful
submission means queued, not acknowledged or read by the client.

Positive `give` commands use the same corner-bubble system as badge receipts, via
`NotificationDialogMessage` (`currency_reward`, `display=BUBBLE`). The wallet saves the balance
change and `player_currencies.pending_reward_amount` together. Online grants submit a bubble
immediately; offline or failed submissions remain pending across server restarts. Login submits
one accumulated amount per currency, using `player.reward.currency` (default: "You received
%0% %1%."). Command success no longer reports these offline reward notices as skipped. Negative
adjustments retain their immediate moderator-message notice.

Submission clears the pending amount without changing the balance. It is not a client receipt
acknowledgement: a disconnect can hide an already submitted bubble, and a crash between submission
and clearing can repeat a bubble. Neither case grants money twice. Apply `AddPendingCurrencyRewards`
before running this version. Grants made before that migration have no pending receipt.

Login/reconnect reports current hotel chat/trading restrictions. During an active permission
grain lifetime, expiry reports restoration only when the effective permission becomes available;
another remaining denial suppresses that message. Capability resends do not repeat notices, and
reconnect does not replay past expiries.
