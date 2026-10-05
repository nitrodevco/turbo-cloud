# Plugin packets

A plugin can add packets of its own to Turbo's protocol without touching a revision, through
`IExtensionPacketRegistry` (`Turbo.Primitives`). It is the extension side of
`docs/client-capabilities.md`: a client that never asks for a plugin's capability never receives
its packets.

## Rules

- Headers are `20000`-`32767` except Turbo's `30000`-`30099` (`ExtensionPackets.IsPluginHeader`).
  A header outside that space is refused with `ArgumentOutOfRangeException`.
- One parser per header and one serializer per composer type, across all plugins; a second claim
  is refused with `InvalidOperationException`. Name headers and capabilities after your plugin.
- A capability name is not empty and not a core name (`permission.nodes`, `chat.commands.v2`).
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
capability. The parsed message is a normal `IMessageEvent`, handled by an `IMessageHandler<T>` in
the plugin. A client may send an extension packet whether or not it accepted the capability, so a
handler should check that first.

A registered capability is accepted at version 1 when a client asks for it
(`ClientCapabilities.Negotiate`); core names keep their own versions.
