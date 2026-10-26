# Plugin packets

A plugin can add packets of its own to Turbo's protocol without touching a revision, through
`IExtensionPacketRegistry` (`Turbo.Primitives`). It is the extension side of
`docs/client-capabilities.md`: a client that never asks for a plugin's capability never receives
its packets.

For a new plugin, prefer **named messages** (`docs/plugin-messages.md`): a record and a name, with
the header, the codec and the client's map handled by the host. The registry below stays for
plugins that number their own packets; named messages take their headers from the top of the
plugin space and skip every header registered here.

## Rules

- Headers are `20000`-`32767` except Turbo's `30000`-`30099` (`ExtensionPackets.IsPluginHeader`).
  A header outside that space is refused with `ArgumentOutOfRangeException`.
- One parser per header and one serializer per composer type, across all plugins; a second claim
  is refused with `InvalidOperationException`. Name headers and capabilities after your plugin.
- A capability name is not empty and not a core name (`permission.nodes`, `chat.commands.v2`,
  `client.modules`, ...: every name in `ClientCapabilities.SUPPORTED`).
- Core is looked up first, then the registry, so a plugin cannot replace a core packet. A header
  nobody knows is logged as unknown, as before.
- Extension packets are shared by every revision, so write serializers against the wire layout.

## Usage

Take the registry from the host services in `StartAsync`, register, and dispose the handles in
`StopAsync`. The registry does not remove anything itself.

```csharp
private readonly List<IDisposable> _registrations = [];

public Task StartAsync(IServiceProvider services, CancellationToken ct)
{
    var registry = services.GetRequiredService<IHostServices>()
        .GetRequiredService<IExtensionPacketRegistry>();

    _registrations.Add(registry.RegisterCapability("myplugin.hud"));
    _registrations.Add(registry.RegisterParser(20005, new RequestHudParser()));
    _registrations.Add(registry.RegisterSerializer(typeof(HudStateMessage), new HudStateSerializer()));

    return Task.CompletedTask;
}

public Task StopAsync(CancellationToken ct)
{
    foreach (var registration in _registrations)
        registration.Dispose();

    _registrations.Clear();

    return Task.CompletedTask;
}

public sealed record HudStateMessage(int Value) : IComposer, ICapabilityComposer
{
    public string Capability => "myplugin.hud";
}

internal sealed class HudStateSerializer() : AbstractSerializer<HudStateMessage>(20006)
{
    protected override void Serialize(IServerPacket packet, HudStateMessage message) =>
        packet.WriteInteger(message.Value);
}
```

A composer that implements `ICapabilityComposer` goes only to sessions that accepted its
capability. Send it as any other: to a player with `grainFactory.SendComposerToPlayerAsync`, to a
room with `IRoomGrain.SendComposerToRoomAsync` (or a room object's or wired context's
`SendComposerToRoomAsync`). The room writes a plugin's composer with its registered serializer
before its stream carries it (`WrittenExtensionPacket`), as the host's serializer knows no plugin's
types; a composer with no capability then goes to every session in the room. The parsed message is a normal `IMessageEvent`, handled by an `IMessageHandler<T>` in
the plugin. `IMessageHandler<T>`, `IMessageBehavior<T>` (packet middleware, ordered by `[Order]`)
and the `MessageContext` a handler answers through are contracts in `Turbo.Primitives.dll`, under
the namespace they always had (`Turbo.Messages.Registry`), so a plugin with packet handlers needs
no `Turbo.Messages` reference.
Like a core handler, it is reached only from a session that has logged in (`SignInGate`,
`docs/permissions.md` §11), unless it carries `[AllowAnonymous]`. A client may send an extension
packet whether or not it accepted the capability, so a handler should check that first, for the
session the packet came on:

```csharp
public sealed class RequestHudHandler(IGrainFactory grainFactory) : IMessageHandler<RequestHudMessage>
{
    public async ValueTask HandleAsync(RequestHudMessage message, MessageContext ctx, CancellationToken ct)
    {
        if (!await grainFactory.SessionAcceptsCapabilityAsync(ctx.PlayerId, ctx.SessionKey, "myplugin.hud", ct))
            return;

        // ...
    }
}
```

`SessionAcceptsCapabilityAsync` is false for a session that never asked for the capability, and
for one that is no longer the player's session; `IPlayerPresenceGrain.GetClientCapabilityVersionAsync`
gives the agreed version, 0 when there is none.

A registered capability is accepted at version 1 when a client asks for it
(`ClientCapabilities.Negotiate`); core names keep their own versions.

A plugin's client half, the code that runs in the client and speaks these packets, is a client
module served from the plugin's own folder: `docs/client-modules.md`.
