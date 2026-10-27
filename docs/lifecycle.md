# Packet Lifecycle

- [Introduction](#introduction)
- [Lifecycle Overview](#lifecycle-overview)
    - [First Steps: the Connection and the Revision](#first-steps-the-connection-and-the-revision)
    - [Parsing: Bytes Into a Message](#parsing-bytes-into-a-message)
    - [Behaviours](#behaviours)
    - [Gates](#gates)
    - [Handlers](#handlers)
    - [Grains and Domain Services](#grains-and-domain-services)
    - [Policies and Events](#policies-and-events)
    - [Composers: the Reply](#composers-the-reply)
- [One Packet, End to End](#one-packet-end-to-end)
- [Where Plugins Join In](#where-plugins-join-in)
- [Focus on Handlers](#focus-on-handlers)

## Introduction

Every click in the client is a packet, and every packet takes the same road through the server:
parsed by the client's revision, passed through behaviours and gates, handled, acted on by a grain,
and answered with a composer. This page follows one packet from the moment the client sends it to
the moment the client gets its answer, naming the code at each step, so that the rest of the
documentation has a picture to hang on.

Don't worry if some of the words are new yet: each step links to the page that covers it.

## Lifecycle Overview

```
client ─▶ parser ─▶ behaviours ─▶ gates ─▶ handler ─▶ grain or service
                                                          │
client ◀─ serializer ◀──────────────────── composer ◀─────┘
```

### First Steps: the Connection and the Revision

A client connects to the game's TCP listener (the Flash client) or its WebSocket listener
(nitro-next). The first thing it sends is its **revision**, the client build it is, and the
session keeps it. A revision is a folder under `Turbo.Revisions/` (today `Revision20260909/`)
with the build's header numbers (`Headers.cs`), a **parser** for each packet the client sends and
a **serializer** for each one the server sends. Everything after this step is the same whatever
the revision: no handler, grain or plugin ever sees a header number.

### Parsing: Bytes Into a Message

Each framed packet reaches `PackageHandler`
([`Turbo.Networking/Package/PackageHandler.cs:42`](../Turbo.Networking/Package/PackageHandler.cs)),
which asks the session's revision for the header's parser, then the plugin packets a plugin
registered for a header the revision lacks. The parser reads the bytes into an **incoming
message**, an immutable record in `Turbo.Primitives/Messages/Incoming`:

```csharp
internal class CanCreateRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new CanCreateRoomMessage();
}
```

A header nobody knows is logged as `Incoming Unknown` and dropped. The message is then published to
the message system, which builds a `MessageContext` for it: the session, the signed-in player's id
and the room the player is in, taken from the session without a grain call
([`Turbo.Messages/Registry/MessageRegistry.cs:30`](../Turbo.Messages/Registry/MessageRegistry.cs)).

### Behaviours

A **behaviour** (`IMessageBehavior<T>`) is middleware around a message's handlers. Behaviours run
in their `[Order]`, and each passes the message on by awaiting `next`, or stops it by returning
without calling it:

```csharp
[Order(10)]
public sealed class CreateRoomAudit(ILogger<CreateRoomAudit> logger) : IMessageBehavior<CanCreateRoomMessage>
{
    public async ValueTask InvokeAsync(CanCreateRoomMessage env, MessageContext ctx, Func<ValueTask> next, CancellationToken ct)
    {
        logger.LogInformation("Player {Player} opened the create room window", ctx.PlayerId);

        await next(); // the handlers run; return without this to stop the message here
    }
}
```

> [!WARNING]
> Behaviours fail closed: one that throws before it calls `next` drops the message, and nobody is
> answered ([`IBehavior.cs`](../Turbo.Primitives/Pipeline/Registry/IBehavior.cs)). A rule about a
> game action (a purchase, a room entry) belongs in that action's [policy](plugin-policies.md),
> which answers the client for you.

### Gates

Every handler is wrapped, as it is registered, in two gates
([`Turbo.Messages/Registry/MessageGates.cs`](../Turbo.Messages/Registry/MessageGates.cs)):

1. **Signed in.** A message from a session with no signed-in player never reaches the handler,
   unless the handler carries `[AllowAnonymous]` (the handshake, the SSO ticket, pings).
2. **Permission.** A handler with `[RequiresPermission("node")]` is reached only by a player holding
   one of its nodes; anyone else is skipped without a reply.

So a handler never checks either itself. See [Permissions](permissions.md).

### Handlers

A **handler** (`IMessageHandler<T>`) is where the server decides what a message means. It is
orchestration only: it guards its input, calls a grain or a domain service, and maps what comes
back onto a composer. It never queries the database and never writes to a socket:

```csharp
public class CanCreateRoomMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<CanCreateRoomMessage>
{
    public async ValueTask HandleAsync(CanCreateRoomMessage message, MessageContext ctx, CancellationToken ct)
    {
        var (canCreate, roomLimit) = await navigatorService
            .CanCreateRoomAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new CanCreateRoomMessageComposer
                {
                    Result = canCreate ? RoomCreationResultType.Allowed : RoomCreationResultType.RoomLimitReached,
                    RoomLimit = roomLimit,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
```

A message can have several handlers (core's and a plugin's); they run side by side. One that throws
is logged with the packet and the session, and the others still run
([`Turbo.Pipeline/EnvelopeHost.cs`](../Turbo.Pipeline/EnvelopeHost.cs)).

### Grains and Domain Services

The hotel's state lives in **grains**: a player, a room, a wallet, an inventory, each an Orleans
virtual actor that handles one call at a time. A handler asks the grain (`IRoomGrain`,
`IPlayerWalletGrain`) or a domain service that does (`INavigatorService`), and the grain does the
work in its own **turn**, with its state to itself. A room's furni logic, wired, commands and room
event listeners all run inside the room's turn. [Grains & Orleans](grains.md) covers what that
means for your code.

### Policies and Events

Before a grain commits an action a plugin may take part in (a purchase, a room entry, placing a
furni, a registration), it asks that action's **policies**, which may allow it, refuse it with a
reason the player sees, or change it. After the action is saved, it publishes a **post-commit
event** (`PurchaseCompletedEvent`, `RoomEnteredEvent`) that plugins hear off the grain's turn.
See [Policies & Events](plugin-policies.md).

### Composers: the Reply

What the server sends is a **composer**, an immutable record in
`Turbo.Primitives/Messages/Outgoing`. There are exactly three ways to send one:

| To | Call |
| --- | --- |
| the session that sent the message | `ctx.SendComposerAsync(composer, ct)` |
| a player, wherever they are | `grainFactory.SendComposerToPlayerAsync(...)` / `SendComposerToPlayersAsync(...)` |
| everyone in a room | `RoomGrain.SendComposerToRoomAsync(...)`, from inside the room |

On the way out, the session's revision finds the composer's **serializer**
([`Turbo.Networking/Package/PackageEncoder.cs:106`](../Turbo.Networking/Package/PackageEncoder.cs)),
then a plugin's, which writes it with the revision's header:

```csharp
internal class CanCreateRoomMessageComposerSerializer(int header)
    : AbstractSerializer<CanCreateRoomMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CanCreateRoomMessageComposer message)
    {
        packet.WriteInteger((int)message.Result).WriteInteger(message.RoomLimit);
    }
}
```

The bytes are cached per composer, so a room-wide message is serialized once however many players
receive it.

## One Packet, End to End

The client's "create room" window asks whether the player may create a room:

| Step | Where |
| --- | --- |
| The client sends header `2540` | `Turbo.Revisions/Revision20260909/Headers.cs:51` |
| Its parser makes a `CanCreateRoomMessage` | `Revision20260909.cs:927`, `Parsers/Navigator/CanCreateRoomMessageParser.cs` |
| The handler asks the navigator service | `Turbo.PacketHandlers/Navigator/CanCreateRoomMessageHandler.cs` |
| The service counts the player's rooms and reads their limit | `Turbo.Navigator/NavigatorService.cs:411` |
| The handler answers with a `CanCreateRoomMessageComposer` | `Turbo.Primitives/Messages/Outgoing/Navigator/CanCreateRoomMessageComposer.cs` |
| Its serializer writes header `3021` | `Headers.cs:673`, `Serializers/Navigator/CanCreateRoomMessageComposerSerializer.cs` |

## Where Plugins Join In

A plugin can step into almost every stage without touching core:

| Stage | A plugin adds | Guide |
| --- | --- | --- |
| Parsing | packets of its own, for a client that asks for them | [Plugin Packets](plugin-packets.md) |
| Parsing | named messages, with no header or parser to write | [Plugin Messages](plugin-messages.md) |
| Behaviours | an `IMessageBehavior<T>` around a core packet | this page |
| Handlers | an `IMessageHandler<T>` beside core's, for a core packet | [Plugin Packets](plugin-packets.md) |
| Grains | furni logic and wired boxes, run in the room's turn | [Furni Logic](furni-logic.md), [Wired](plugin-wired.md) |
| Policies | allow, refuse or change an action | [Policies & Events](plugin-policies.md) |
| Events | react once an action is saved | [Policies & Events](plugin-policies.md) |

Every one of these is a public class in the plugin's assembly, found as the plugin loads and taken
away as it unloads.

## Focus on Handlers

If you remember one thing from this page: **a handler orchestrates**. It doesn't read tables,
doesn't write to sockets, doesn't catch exceptions to log them (the pipeline logs every failure)
and doesn't check that the player is signed in (the gate did). The real handler this page quotes
is mirrored, with every rule explained, in [`docs/patterns/HandlerPattern.cs`](patterns/HandlerPattern.cs).
