---
name: turbo-plugin-messages
description: Make a Turbo plugin's client and server halves talk with named plugin messages - [PluginMessage("<key>.name")] records, IPluginMessageHandler<T>, ReplyAsync and sends to players and rooms, carrying a core packet, and api.messages.on/send with ClientModuleMessageOf in the client module. Use whenever data crosses between a plugin's server code and its client module.
---

# Plugin messages

## When to use this skill

The module asks the server for something, the server pushes state to the module, or the server
relays a core packet. Never hand-number packet headers or write a parser/serializer: the host gives
each message a header, sends clients the name -> header map and the field schema, and both sides
read and write by name.

## Declare and handle (server)

`npm run make message levelled --to client --fields level:int,name:string` writes both halves.

<!-- compiled as the plugin "demo" -->

```csharp
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins.Messages;

namespace Demo;

/// <summary>A nested record: no attribute, positional, public.</summary>
public sealed record Score(string Name, int Points);

/// <summary>Server to client: the board.</summary>
[PluginMessage("demo.board")]
public sealed record BoardMessage(int Total, ImmutableArray<Score> Scores, bool Final) : IPluginMessage;

/// <summary>Client to server: the module asks for the board.</summary>
[PluginMessage("demo.get_board")]
public sealed record GetBoardMessage() : IPluginMessage;

/// <summary>Found as the plugin loads; only signed-in sessions reach it.</summary>
[RequiresPermission("demo.board.view")]
public sealed class GetBoardHandler : IPluginMessageHandler<GetBoardMessage>
{
    public async ValueTask HandleAsync(GetBoardMessage message, PluginMessageContext ctx, CancellationToken ct) =>
        await ctx.ReplyAsync(new BoardMessage(1, [new Score("Alice", 10)], false), ct);
}
```

- The name starts with the plugin's key and a dot; another prefix or a taken name fails the load.
- Fields are the primary constructor's parameters, in order: `int`, `long`, `bool`, `string`,
  `double`, an enum (as int), a record of those, arrays (`T[]`, `ImmutableArray<T>`,
  `IReadOnlyList<T>`), dictionaries keyed by `string`. No nullables (except the carried packet below).
- No direction: any message goes either way, except one carrying a packet (server to client only).
- A handler is orchestration only: call the plugin's service; `ctx.PlayerId`, `ctx.RoomId`,
  `ctx.SessionKey`, `ctx.ReplyAsync`. `[RequiresPermission]` and `[AllowAnonymous]` work as on packets.

## Send (server)

| To | Call |
| --- | --- |
| the session being handled | `ctx.ReplyAsync(message, ct)` |
| a player anywhere | `hotel.Players.SendAsync(player, message, ct)` (false when offline) |
| everyone in a room | `hotel.Rooms.SendAsync(room, message, ct)` |
| several players / from grain code | `grainFactory.SendPluginMessageToPlayersAsync(ids, message, ct)`; a room send that takes a composer: `PluginMessageComposer.For(message)` |

A message reaches only clients that accepted `client.messages` (nitro-next does).

## Carrying a core packet

The last field may be an `IComposer?` (`Turbo.Primitives.Networking`): a whole hotel packet after the
other fields. A room watch fills it for you (turbo-room-holds-watches skill).

```csharp
using Turbo.Primitives.Networking;
using Turbo.Primitives.Plugins.Messages;

namespace Demo;

[PluginMessage("demo.room")]
public sealed record DemoRoomMessage(int RoomId, IComposer? Carried = null) : IPluginMessage;
```

The client gets that field as `{ header, packet }`, read by its own parser; only a secondary room's
`apply` uses it (turbo-client-modules skill).

## Read and send (client module)

```ts
import { defineModule, type ClientModuleMessageOf } from '@nitrodevco/client-module-sdk';

// The record's fields as the server sends its schema: camelCase, in order.
export const BOARD = 'demo.board';
export const BOARD_SCHEMA = [ [ 'total', 'int' ], [ 'scores', [ 'array', [ 'object', [ [ 'name', 'string' ], [ 'points', 'int' ] ] ] ] ], [ 'final', 'bool' ] ] as const;
export type Board = ClientModuleMessageOf<typeof BOARD_SCHEMA>;

export default defineModule({
    activate(api) {
        api.messages.on(BOARD, data => console.log((data as Board).total));
        api.events.on('room.entered', () => api.messages.send('demo.get_board', {}));
    },
});
```

A field the client was not told of is ignored and a missing one gets its default, logged once: a
mismatch is reported, never misread. Keep the parsing of `data` in a plain `.ts` file
(`client/tests` covers it with `node --test`).

## Check

`npm run check`; the dev hotel's MCP tool `message_schema` shows the header and fields the hotel
gave a name. Guide: `plugin-messages.md`; client side nitro-next `docs/client-modules.md`.
