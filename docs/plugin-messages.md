# Plugin messages

A plugin's server half and its client half talk through **named messages**: the plugin declares a
message once, in C#, as a record with a name. The host gives it a header, writes and reads it from
its fields, and tells clients the name, the header and the fields, so the client module reads and
writes it by name. Nobody numbers a header, writes a serializer or a parser, or copies a constant
into JavaScript.

Hand-numbered extension packets (`docs/plugin-packets.md`) keep working beside them; named messages
are the way to add new ones.

## 1. Declaring a message

```csharp
using System.Collections.Immutable;
using Turbo.Primitives.Plugins.Messages;

public sealed record Skill(string Name, int Level, long Xp);

[PluginMessage("skills.state")]
public sealed record SkillsState(int Total, ImmutableArray<Skill> Skills, bool Max) : IPluginMessage;

[PluginMessage("skills.train")]
public sealed record TrainSkill(string Skill) : IPluginMessage;
```

- The name starts with the plugin's key and a dot (`skills.` for the plugin `skills`). Another
  prefix refuses the plugin's load, as does a name another plugin already registered.
- The fields are the record's primary constructor parameters, in order. A field is an `int`, `long`,
  `bool`, `string`, `double`, an enum (sent as its int; one whose values do not fit an int is
  refused), a record of those (no attribute, not an `IPluginMessage`: a positional record with one
  public constructor), an array of any of them (`T[]`, `ImmutableArray<T>`, `IReadOnlyList<T>`), or
  a dictionary keyed by `string` of any of them (`Dictionary<string, T>`, `IDictionary<string, T>`,
  `IReadOnlyDictionary<string, T>`, `ImmutableDictionary<string, T>`); the client reads a dictionary
  as a plain object. Anything else (a dictionary with other keys, a nullable, a record that contains
  itself) refuses the plugin's load, and the message names the field
  (`skills.state.Skills[].Name: ...`).
- A message has no direction: any message can be sent either way.
- Messages, nested records and handlers must be `public` to be found.

The plugin needs nothing but `Turbo.Contracts` and `Turbo.Primitives`.

## 2. Handling one a client sends

```csharp
[RequiresPermission("skills.train")]
public sealed class TrainSkillHandler(IGrainFactory grainFactory) : IPluginMessageHandler<TrainSkill>
{
    public async ValueTask HandleAsync(TrainSkill message, PluginMessageContext ctx, CancellationToken ct)
    {
        // ... ask the plugin's grain or service to train it, then:
        await ctx.ReplyAsync(new SkillsState(1, [new Skill(message.Skill, 2, 150)], false), ct);
    }
}
```

A handler is found in the plugin's assembly as a packet handler is, and runs through the same packet
pipeline: only a signed-in session reaches it unless it carries `[AllowAnonymous]`, and
`[RequiresPermission]` is honoured (`docs/permissions.md` §11). Keep it orchestration only.
`PluginMessageContext` gives `PlayerId`, `RoomId`, `SessionKey` and `ReplyAsync`, which answers the
session the message came from.

## 3. Sending one

The three ways to send a composer (`AGENTS.md`: the session handled, a player, a room) each take a
plugin message too:

| To | Call |
| --- | --- |
| the session being handled | `ctx.ReplyAsync(message, ct)` |
| a player, wherever connected | `grainFactory.SendPluginMessageToPlayerAsync(playerId, message, ct)` |
| several players | `grainFactory.SendPluginMessageToPlayersAsync(playerIds, message, ct)` |
| everyone in a room | `roomGrain.SendPluginMessageToRoomAsync(message, ct)` |

Code that holds another room send (a room object's, a wired context's `SendComposerToRoomAsync`)
passes `PluginMessageComposer.For(message)` to it. `PluginMessageComposer` is the message written
by its schema, a name and bytes, so it crosses grain calls and room streams whatever the plugin's
record is; the session's encoder frames it with the header the name has. It goes only to a session
that accepted `client.messages` (§5); a message whose plugin has gone by then is dropped and logged.

### Carrying a core packet

`PluginEnvelopeComposer.For(message, packet)` sends your message with a whole packet of the
hotel's after its fields (another room's furni or avatars, relayed): its short header and body as
the session's encoder writes it, for the client module to read with `api.packets.parseCore`. The
carried packet is written once for every session it goes to. A room watch builds these for you;
see `docs/plugin-room-watch.md`.

## 4. Headers

Headers are the host's business. Each message gets one header, the same both ways, from the plugin
space (`20000`-`32767` outside Turbo's `30000`-`30099`), **from the top down**, past every header a
hand-numbered plugin packet already uses (`IExtensionPacketRegistry.HasHeader`). Hand-numbered
plugins conventionally start at 20000, so the two meet only when the space is full. A header a
message reads is registered in the extension registry, so a hand-numbered plugin that loads later
and asks for it is refused (`InvalidOperationException`).

A name keeps its header for the life of the process: a plugin that reloads gets its old headers back
while nothing else took them, and a header another name once had is given last. Clients never rely
on a header; they are sent the map.

Turbo's own modules declare named messages the same way, for features no Habbo message carries,
instead of a hand-numbered header: their names start `turbo.` (the owner `turbo`), any other name
outside a plugin is refused. Today: `turbo.theme.choose` / `turbo.theme.chosen`, a player's choice of
theme (`docs/plugin-data.md`, "Who gets which theme"), and `turbo.theme.changed { theme, revision, url }`,
the player's look changed (`docs/plugin-data.md`, "Live reload").

## 5. The protocol: `client.messages` (version 1)

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| server → client | 30010 | `TurboPluginMessagesMessage` | `int count`, then per message `string name`, `int header`, `string schema` |

Sent straight after the capability answer to a client that accepted `client.messages`, and again to
every such client online whenever the set changes (a plugin with messages loads, unloads or
reloads): always the whole set, sorted by name.

A plugin message itself is `int length`, `short header`, then its fields in order:

| Schema type | Wire |
| --- | --- |
| `"int"` | 4 bytes big-endian (an enum: its int value) |
| `"long"` | 8 bytes big-endian (Habbo's `Long`: high int, then low int) |
| `"bool"` | 1 byte, 1 or 0 |
| `"string"` | `short` byte length, then UTF-8 |
| `"double"` | 8 bytes, IEEE 754 big-endian |
| `["array", T]` | `int` count, then each item as `T` |
| `["map", T]` | `int` count, then per entry its key as a `"string"` and its value as `T`, in the dictionary's order (a key sent twice keeps its last value) |
| `["object", fields]` | each field in order, nothing around them |

The **schema** is compact JSON: an array of `[name, type]` fields, names in camelCase, where a type
is one of `"int"`, `"long"`, `"bool"`, `"string"`, `"double"`, `["array", type]`,
`["map", type]` or `["object", fields]`. `SkillsState` above is:

```json
[["total","int"],["skills",["array",["object",[["name","string"],["level","int"],["xp","long"]]]]],["max","bool"]]
```

A generator can turn the schema into TypeScript types: `"int"`, `"long"` and `"double"` are
`number`, `"bool"` is `boolean`, `"string"` is `string`, an array is `T[]`, a map
`Record<string, T>` and an object an interface of its fields.

A client reads and writes by the schema it was sent, so a mismatch between the halves is detected,
not misread: nitro-next ignores a field it was not told of and fills a missing one with its default
(`0`, `''`, `false`, `[]`, `{}`), logging each once per name. The server reads a client's message strictly
by its own schema; a packet too short for it is logged and dropped, and an array count past what
the packet holds (an array's or a map's) is refused before anything is allocated.

## 6. Unloading

Everything a plugin's messages registered goes when it unloads, reloads or fails to load, whether
or not it disposed anything: the names, their parsers, their headers and its handlers. Clients are
sent the set without them.

## 7. What it added to the server

- `Turbo.Primitives`, namespace `Turbo.Primitives.Plugins.Messages`: `IPluginMessage`,
  `PluginMessageAttribute`, `IPluginMessageHandler<T>`, `PluginMessageContext`, `PluginMessageSchema`
  (the schema and the generic codec), `PluginMessageComposer`, `PluginMessageValueKind`,
  `IPluginMessageDirectory`, `PluginMessageSnapshot`, `PluginMessageRoomExtensions`;
  `SendPluginMessageToPlayerAsync` / `ToPlayersAsync` beside `SendComposerToPlayerAsync`;
  `TurboPluginMessagesMessage`; `ClientCapabilities.CLIENT_MESSAGES`;
  `IExtensionPacketRegistry.HasHeader`.
- `Turbo.Messages`: `PluginMessageDirectory` (headers, parsers, the list), `PluginMessageFeatureProcessor`
  (the assembly scan), `PluginMessageBroadcast` (the list to everyone online on a change), and
  `MessageGates`, the sign-in and permission gates both kinds of handler share. The permission gate
  now asks the host's grain factory: a plugin's container has none, so a plugin packet handler with
  `[RequiresPermission]` used to fail instead of being checked.
- `Turbo.Networking`: `PackageEncoder` frames a `PluginMessageComposer` with its name's header.
- `Turbo.Revisions`: header 30010 in `Revision20260909`.

Tests: `Turbo.Tests/Plugins/PluginMessageTests.cs` (codec round trip of every type, the wire
encoding, the schema, refusals, header assignment, a compiled plugin's handler reached from client
bytes through the real packet handler, the gates, sending to players and rooms, the capability gate,
the list after the capability answer and on unload).
