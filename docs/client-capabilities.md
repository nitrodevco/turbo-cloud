# Client capabilities: Turbo's protocol extensions

Turbo speaks Habbo's protocol to any client, and nitro-next speaks it to any emulator. Some
things the Habbo protocol cannot say; for those Turbo offers **extensions**, which a client asks
for by name. A client that never asks (the Flash client, any other port) never sends or receives
an extension packet, so neither side depends on the other.

## The exchange

After `AuthenticationOK` the client may send:

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| client → server | 30000 | `TurboClientCapabilitiesMessage` | `int count`, then per extension `string name`, `int version` |
| server → client | 30000 | `TurboServerCapabilitiesMessage` | the same shape: each extension accepted, at the version both will use |

The server accepts each extension it speaks, at the lower of the two versions
(`ClientCapabilities.Negotiate`), ignores names it does not know, and answers even when it accepts
none. Acceptance belongs to the session: the presence grain keeps it, and clears it when a session
attaches or leaves. An extension's composers implement `ICapabilityComposer`, naming their
extension, and the presence drops one bound for a session that did not accept it. The grains that
compose them send unconditionally and never ask what the client speaks. A server that does not know header 30000 logs and ignores it, and never
answers: the client keeps behaving as a plain Habbo client.

Headers 30000–30099 are reserved for Turbo's own extensions, in every revision. Habbo's own ids
stop at 4101 in revision 20260909 and grow upward; headers 0–19999 stay with Habbo and core.
Plugins may register their own extensions in 20000–32767 outside 30000–30099
(`docs/plugin-packets.md`); a plugin capability is accepted at version 1.

## `permission.nodes` (version 1)

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| server → client | 30001 | `TurboPermissionNodesMessage` | `int count`, then `string node` per node: the whole set |

Sent straight after the capability answer, then whenever the player's client-facing nodes change,
after `UserRights`/`PerkAllowances` when those changed too. Each of the three goes only when what it
says changed: a node the level already covered sends the node list alone. It carries every node the player holds that a client
gates on: every node with a `ClientLevel` (the Flash `hasSecurity` threshold of its gate), and any
node a plugin registered with `ClientVisible: true`. Server-only nodes (`room.enter.full`,
`chat.speak`), perks and membership nodes are not sent; perks travel in `PerkAllowances` as ever.

A client with the list gates each feature on its node. Without it, it gates on the level, as Flash
does. The level is still sent and still derived from the nodes (`docs/permissions.md` §8), so the
two agree whichever a client reads.

The node names are part of this extension's contract: renaming a node that has a `ClientLevel` is a
protocol change, and nitro-next's gate table (`packages/nitro-react/src/context/user/gates/ClientGate.ts`)
names the same strings.

## `chat.commands.v2` (version 1)

**Status: built on the server and in nitro-next.** The v2 name deliberately requires a new negotiation; the old `chat.commands` layout is not accepted. Older clients continue to send ordinary chat commands without this completion extension. It gives a client the chat
commands as data, so it can complete, check and explain a line while the player types, instead of
the server whispering an error after they press enter.

### Why

Every command already declares its arguments as a typed record (`docs/commands.md` §3). Core knows
each one's name, aliases, category, room level, and the type of every parameter: a player in the
room, a player anywhere, a duration, an enum, a number, the rest of the line. Today that schema
never leaves the server; it only parses a line after it has been sent. Minecraft's command system
(Brigadier, since 1.13) sends the same kind of description to the client, filtered to what that
player may use. The client completes from it, marks the wrong word before the line is sent, and
asks the server only for values it cannot know. This extension is that, in Habbo's protocol.

What it is not: the server stays the authority. A client that completes and checks a line still
sends it as chat, and the server binds, checks and refuses it exactly as it does now. A client
that does not accept the extension loses nothing.

### Packets

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| server → client | 30002 | `TurboCommandTreeMessage` | the commands the player may use, below |
| client → server | 30003 | `TurboCommandSuggestMessage` | `int requestId`, `string command`, `int parameter`, `string prefix`, `string syntax`, `string argumentText` |
| server → client | 30004 | `TurboCommandSuggestionsMessage` | `int requestId`, `int count`, then `string value` per suggestion |

**`TurboCommandTreeMessage`** is the whole set, never a change to it:

```
int commandCount
  string name
  int aliasCount, then string alias       declared and hotel aliases, one list
  string category
  string description                      hotel text command.<name>.description, else the declared one
  string usage                            ":ban <who> <duration> [reason]", as the server's own replies say it
  int roomLevel                           RoomControllerType the command needs; -1 for none
  bool operator                           runs outside the room; the room level never applies
  int parameterCount
    string name
    int kind                              see below
    bool optional
    int suggest                           0 none, 1 from the client's own data, 2 ask the server
    bool selectors                        a player parameter this player may aim at @room and @online
    int memberCount, then string member   an enumeration's members; 0 for any other kind
    string description
    string minimum, string maximum        decimal integer bounds; empty when absent
    int minLength, int maxLength           -1 when absent
    string defaultValue                   empty when absent or sensitive
  int syntaxCount
    string path                           space-separated literal prefix, e.g. "add"
    string usage
    int parameterCount, then parameters   same complete parameter shape as above
```

The player's controller level in each room is already sent (`RoomRightsMessage` and friends), so
the tree is the same in every room and is not resent on entry: the client hides a command whose
`roomLevel` it does not reach where it is, and shows it again where it does.

**Parameter kinds** are part of the contract. A client that meets a kind it does not know treats it
as a word.

| Kind | Server type | The client can check | Suggestions |
| --- | --- | --- | --- |
| 0 word | `string` | nothing | from the server, when the parameter names a source |
| 1 integer | `int` | digits, an optional plus or minus, the range of an int | none |
| 2 long | `long` | as integer | none |
| 3 boolean | `bool` | `true`, `false`, `on`, `off`, `yes`, `no`, `1`, `0` | the client lists them |
| 4 enumeration | an enum | membership, ignoring case | the members, which the tree carries |
| 5 room player | `IRoomPlayer` | that the name is in the room | the client's own room user list |
| 6 player | `PlayerTarget` | nothing: the player may be offline | from the server |
| 7 duration | `CommandDuration` | a positive number and `m`, `h`, `d` or `w`, capped at 3650 days, or `perm` / `permanent` | `30m`, `1h`, `1d`, `7d`, `perm` |
| 8 rest of line | `RestOfLine` | nothing | none |

**`TurboCommandSuggestMessage`** asks for the values of one parameter that begin with `prefix`.
`parameter` is its index in the selected syntax branch (or root parameters). Requests append `string syntax` (empty for root) and `string argumentText` (the preceding arguments in their original quoted form). The context has a 1024-character protocol bound; names, paths and prefixes have a 64-character bound. The answer echoes `requestId`, so a client that has moved on
drops a stale one. The server answers with at most 20 values, sorted, and answers with none when:

- the player may not use the command: the same node check `:commands` makes, so a suggestion never
  tells a player more than the command would;
- the parameter's `suggest` is not 2, or the index is out of range;
- the prefix of a player parameter is shorter than 2 characters: a single letter would page through
  the hotel's player list;
- the player is over the flood limit below.

Suggestions do not count against chat flood: a client asks as the player types, not as they speak.
They have their own small limit, `Turbo:Commands:SuggestionsPerSecond` (default 5). A request over
it is answered with none, so the client's request never hangs.

### Where suggestions come from

A player parameter (kind 6) is answered from the player directory by name prefix, online players
first. Selectors are offered as well, `@room` and `@online`, but only when the player holds the
command's selector node (below).

A word parameter names its source with an attribute. A source is a class implementing
`ISuggestionSource`, found in an assembly as a command is, so a plugin's come and go with it, and a
name claimed twice fails the second load:

```csharp
public sealed record GiveArguments(
    PlayerTarget Who,
    [Suggest(SuggestionSources.CURRENCIES)] string Currency,
    int Amount
);
```

| Source | Values | Used by |
| --- | --- | --- |
| `currencies` | enabled `currency_types` names | `:give` |
| `furni` | furni definition names (a sorted array, searched by halving) | `:giveitem` |
| `groups` | permission group names | `:group` |
| `nodes` | registered permission nodes | `:perm check` |

`ISuggestionSource` receives `CommandSuggestionContext`, prefix, limit and cancellation.
The context includes the executor, active room, current permissions, syntax path and preceding
argument values. Previous arguments remain untrusted advisory text. The service checks both
command and branch permissions before calling a source; execution validates them again.
`group remove` uses the preceding player to offer active direct memberships.

Command branches carry their own parameters and extra permission. Unauthorized branches are
omitted from both usage and syntax. A permission change affecting branch visibility resends the
tree. The client uses source ranges to replace the argument under the cursor while preserving
following arguments. Double-quoted strings escape a quote or backslash with a backslash.

### Selectors are declared

A command names its selector node on the parameter, not in its body, so the tree can tell whether a
player may use `@online` with it without running it. `:commands` already uses this:

```csharp
public sealed record GiveArguments(
    [Selectors(PermissionNodes.Command.GIVE_MASS)] PlayerTarget Who, ...
);
```

The binder reads it at registration (`ICommandBinder.SelectorNode`, `SelectorParameter`) and
`ctx.SelectAsync(who, ct)` takes the node from there. A `PlayerTarget` without the attribute takes
no selector. In the tree, every parameter carries `bool selectors`: true when the player holds
the node, so the client offers `@room` and `@online` only to those who may use them. The
suggestion service offers them too, for a prefix starting with `@`, on the same condition.

### When the tree is sent

- Straight after the capability answer, after `TurboPermissionNodesMessage`.
- When the player's resolved permissions change, from the same place `permission.nodes` is resent
  (`PlayerPermissionGrain.ClientSync`), and only when the commands they may use changed.
- When the registry changes: a plugin loads or unloads, or the hotel texts reload and bring new
  aliases or descriptions. `ICommandRegistryProvider.Changed` already fires for each; a listener
  sends every online player their tree again. A player who did not accept the extension is dropped
  by the presence, as for any `ICapabilityComposer`, so the listener never asks.

A tree is a few kilobytes for a staff member holding every core command, and a few hundred bytes for
a player holding `:commands`, `:kick` and `:mute`. It is built per player from the registry and the
resolved permissions; nothing is cached per player.

### The client's own commands

A client runs some `:words` itself (§1 of `docs/commands.md`: about 70 in AIR, 6 in nitro-next) and
never sends them. Its own commands win over the tree's, as they do today: nitro-next lists both,
completes both, and runs its own locally. The server's registry already warns when a command takes
a name AIR keeps for itself.

### What it changed on the server

- `ICommandBinder.Parameters` exposes what the binder reads at registration (`CommandParameterInfo`:
  name, `CommandParameterKind`, optional, enum members, suggestion source, selector node).
  `[Suggest]` on anything but a `string` is refused at registration.
- `CommandTreeBuilder` builds `CommandTreeSnapshot` as a pure function of the registry, the
  resolved permissions and the hotel texts; its `Key` is what decides whether a permission change
  needs a new tree.
- `CommandTreeService` sends it; `CommandTreePermissionsHandler` listens to
  `PlayerPermissionsChangedEvent`; the service listens to `ICommandRegistryProvider.Changed`.
- `CommandSuggestionService` answers requests, `SuggestionSourceRegistry` holds the sources, and
  `TurboCommandSuggestMessageHandler` only orchestrates.
- `IPlayerDirectoryGrain.SearchNamesAsync` (a `LIKE 'prefix%'` the name index answers, the prefix's
  own wildcards escaped) and `IFurnitureDefinitionProvider.FindNames`.
- Headers 30002 to 30004 in `Revision20260909`, and `chat.commands.v2` at version 1 in
  `ClientCapabilities.SUPPORTED`. A prefix or command name longer than 64 characters is cut.
- Tunables under `Turbo:Commands`: `SuggestionsPerSecond`, `MaxSuggestions`, `MinPlayerPrefix`.

### Tests

- The tree carries exactly the commands `:commands` lists for the same player, with the same usage
  line: one test runs both and compares them, so the two can never drift.
- A command the player may not use is in neither the tree nor its suggestions.
- Parameter kinds and enum members round-trip through the serializer as the client reads them.
- A permission change, a plugin unload and a texts reload each send a new tree, and a change that
  leaves the commands as they were sends none.
- The suggestion limit answers with nothing, not with silence, and is apart from chat flood.

### Open

- **Room level on the client.** The tree relies on the client knowing its controller level per
  room. nitro-next does; a client that does not will offer commands the server then refuses, which
  is harmless.
- **Argument hints from texts.** Parameter names are the record's (`who`, `duration`). A hotel may
  want them translated (`command.<name>.param.<parameter>`); add it when a hotel asks.
- **Confirming a mass action.** Built in the runner (`docs/commands.md` §4.2): a line that reaches
  many players waits for `:confirm`. A client learns it only from the reply; a later version could
  carry a `confirms` flag so the client warns before sending.
- Client checks are advisory. Plugin argument types can impose additional validation on the server.
