# Holding and watching rooms

A plugin that draws one room beside another (a portal that shows the room it leads to, a screen
showing a stage) needs the far room loaded and moving with no one in it, and its packets sent to
players who are not in it. Core keeps rooms the way Habbo's are kept: an empty room goes dormant
(its pets, bots, rollers and periodic wired stop) and unloads 35 s after the last player left, and
a room's packets reach only the players in it. Holds and watches are the two ways around that, and
both end on their own.

## Holds

```csharp
await hotel.Rooms.HoldAsync(room, "lobby-sign", TimeSpan.FromMinutes(2), ct);   // Held
await hotel.Rooms.ReleaseAsync(room, "lobby-sign", ct);
```

`HoldAsync` loads the room if it is not loaded and keeps it loaded and awake for the lease: it ticks
as a room with someone in it does. The same holder name again renews the lease; keep renewing it
while you need the room (a scheduled task, `docs/plugin-scheduler.md`). A hold ends when:

- its lease runs out (1 s to 10 min; a longer one is cut to 10 min),
- you release it (`ReleaseAsync`),
- your plugin unloads (a reload, a disable, the server stopping): core releases everything the
  plugin holds, you do nothing.

Then the room is an empty room again: dormant, and unloaded 35 s later.

The answer is `Held`, `RoomFull` (a room takes 256 holds), `PluginFull` (a plugin holds at most 100
rooms on a server; one it holds already is not one more) or `Failed` (no such room, or it could not
load; logged). The limits are `RoomWatchLimits`.

## Watches

```csharp
[PluginMessage("portals.room")]
public sealed record PortalRoom(int RoomId, IComposer? Carried = null) : IPluginMessage;

await hotel.Rooms.WatchAsync(player, farRoom, new PortalRoom(farRoom.Value), TimeSpan.FromMinutes(1), ct);
```

A watch first sends the player the far room as a room entry draws it, then everything the room
sends the players in it (avatars walking, furni moving, chat), each packet inside your envelope
message (in its last field, the carried packet, which you leave null), in order with their own
room's packets. An envelope message without that field is refused (`ArgumentException`). It holds
the room while it lasts.

The first view is the entry's packets, in the entry's order: RoomReady, RoomEntryTile, the room's
properties, HeightMap, FloorHeightMap, RoomVisualizationSettings, Users, the WIRED visualization
states, Objects, Items, ConfigurationItemStates, FurnitureLabelsAndRegions, then everyone's status
(UserUpdate) and their dances, effects and sleeps. What only someone standing in the room is told
is left out: their own avatar, RoomEntryInfo, the event card, WiredEnvironment, rights and
ownership, WiredPermissions, the rating and the raid protection, and the chat styles. The room
takes the view in one of its turns and numbers its publishes, so nothing it publishes reaches the
player before the view, and nothing the view already shows reaches them again. A renewal before
the lease runs out sends no view; one after it (the watch had ended) starts with a new one. The player watches only a room they could walk into: banned, or kept
out by the door (doorbell, password, a lobby for new users, a room closed or hidden), they are not
let watch it (`Banned`, `DoorClosed`); a full room does not keep a watcher out, since watching takes
no place in it. The check is made again on each renewal, so a ban placed since ends the watch.

A watch ends when its lease runs out, on `UnwatchAsync`, when the player's session ends (logout, a
new login), when they walk into the room (from then on they get its packets as anyone in it does)
or when your plugin unloads. Calling `WatchAsync` again renews it and takes the new envelope. A
player watches at most 4 rooms, each for one plugin (`TooManyWatches`, `WatchedForAnotherPlugin`);
`NotOnline` and `InThatRoom` say why else not.

### Through a linked pair

A portal is usually a linked pair (`docs/plugin-wired.md`, Linked pairs). Its logic lets a player
watch the room its other half stands in from the room's own turn:

```csharp
await ctx.WatchRoomAsync(user, farRoom, new PortalRoom(farRoom.Value), TimeSpan.FromMinutes(1), ct);
```

The door rule is the forward's (`ctx.ForwardToRoomAsync`): a linked pair's half watching the room
its other half stands in passes that room's door, as a teleporter does. Core checks the pair and
the room; the plugin cannot claim it. Bans still apply. Any other watch from a furni is a plain one.
It is asked, not awaited (the room's turn must not wait on another room), so it answers only whether
the user is a player in this room; renew it the same way.

## Envelopes

The envelope (`PluginEnvelopeComposer`, in `Turbo.Primitives.Plugins.Messages`) is a plugin message
whose last field carries a whole packet of the hotel's (`docs/plugin-messages.md`, "Carrying a core
packet"): on the wire, your message's header and other fields, then the carried packet's short
header and body exactly as the session's encoder writes that packet alone (no length). A watch
builds them for you; to send one yourself, send the message with its field set:

```csharp
await hotel.Players.SendAsync(player, new PortalRoom(12, corePacket), ct);
```

The client reads your fields by the schema it was sent, and the carried field with its own parser
for the packet's header: a module gets `{ header, packet }`, `packet` being what the client's parser
read (`undefined`, and a warning, for a header the client has no packet at). It only parses: none of
the client's handlers hear the carried packet; a secondary room takes it with `apply`
(`docs/client-modules.md` in nitro-next). An envelope reaches only a session that accepted
`client.messages`, as any plugin message does, and a carried plugin packet only a session that
accepted its own capability. An envelope cannot carry another envelope.

## Cost

A room's packets are the same objects for every player in it and every watcher, so each is
serialized once (the encoder's payload cache), as a room broadcast is; per watcher only the
envelope's frame is written (6 bytes plus your fields) and one small object queued. Watchers whose
watch starts while the room has published nothing new (and within a room tick) get the same first
view packets, so each of those is written once for all of them too; the encoder keeps packets up to
16 KB, so a big room's Objects is written for each watcher. A held room
costs what a room with someone in it costs: its tick runs at full rate.

## Testing

`FakeHotel` keeps the same rules in memory (`docs/hotel-api.md`): `Rooms.HoldAsync`,
`WatchAsync`, the limits, a `FakeRoom`'s `Banned` and `DoorClosed`, leases by the hotel's clock,
and `Rooms.IsHeld(room)`, `Rooms.Watching(player)`, `Rooms.WatchEnvelope(player, room)` to read
back. A fake hotel is one plugin's.
