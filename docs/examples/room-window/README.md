# Room window: a plugin that uses the room hooks end to end

A window furni bought as a pair, like a teleporter. Put one half in room A and the other in room B:

- a player in A sees room B drawn beside A, greyed, with the window of B standing just behind the
  window of A, and B moving as it moves (players walking, furni moved);
- clicking a floor tile of B walks them up to the window, through it, and into B on the tile they
  clicked, with no loading screen: the client takes the B it was drawing as the room it enters, and
  keeps A drawn behind them;
- the window passes B's door (doorbell, password) as a teleporter does; a player banned from B is
  shown nothing of it and cannot go through.

It is a source-only plugin (`docs/plugin-loading.md`): the server compiles `server/*.cs` and builds
`client/src/index.ts` as it loads the folder.

| File | Hook it shows |
| --- | --- |
| `server/RoomWindowLogic.cs` | `[FurniLogic(LinkedPair = true)]`, `OnAttachedAsync` / `OnDetachedAsync`, `OnMessageAsync`, `PairedItemId`, `GetRoomOfItemAsync`, `WatchRoomAsync`, `WalkToInOrderAsync`, `TileInFront`, `TilesBeside`, `ShowStepAsync`, `ForwardToRoomAsync` with a `RoomEntryExtension` (`docs/plugin-wired.md`, `docs/plugin-room-watch.md`) |
| `server/RoomWindowHandlers.cs` | plugin message handlers asking the furni through `IHotel.Rooms.InvokeFurniLogicAsync`, one room at a time (`docs/plugin-messages.md`) |
| `server/WindowArrival.cs` | `IRoomArrivalPolicy` placing the player on the clicked tile (`docs/plugin-policies.md`) |
| `server/RoomWindowMessages.cs` | the named messages, and the watch's envelope with its carried packet field |
| `server/WindowDirectory.cs`, `server/RoomWindowPlugin.cs` | a plugin service the logic and the handlers share |
| `client/src/index.ts` | `api.messages`, `api.room.createSecondary` + `apply`, `api.room.onUnhandledPointer` (`secondaryTile`), `api.room.keepWhenLeft`, adoption on entry (nitro-next `docs/client-modules.md`, "Secondary rooms") |
| `tests/RoomWindowTests.cs` | the plugin's own tests against `FakeHotel` |

## How a look goes

1. The module sends `roomwindow.look` as the player enters a room, and every 30 s while they stay.
2. `LookHandler` takes the windows of the player's room from `WindowDirectory`, asks each (`look`)
   which room its other half is in and the tile behind it, asks the other half in its own room
   (`where`) where it stands, and answers `roomwindow.views`: each far room and the tile of this room
   its tile (0, 0) is drawn on.
3. Then it asks each window to let the player watch the far room (`watch`). Through its own pair the
   watch passes the far room's door; a ban refuses it. The watch sends the room as an entry draws it,
   then everything it sends its players, each packet inside `roomwindow.room`.
4. The module draws a far room with its first packet, and applies each one to it.

## How a crossing goes

1. A click on a far room's floor gives the module `secondaryTile`; it sends `roomwindow.cross` and asks
   the client to keep the room on screen drawn once the player has left it.
2. `CrossHandler` asks the window (`cross`): it walks the player to the tile in front of it (else
   beside it), shows the step through it and forwards them to the far room with the clicked tile in
   a `RoomEntryExtension`.
3. The far room lets them in (a forward through the window's own pair passes the door; bans, a full
   room and the entry policies still decide), and `WindowArrival` stands them on the clicked tile.
4. The client enters the room it was drawing as it is, and the room left is drawn on behind; the new
   room's windows lead back to it, so its next look feeds it again.

## Trying it

Copy `manifest.json`, `server/` and `client/` into the server's plugins folder as `roomwindow/` (or
name this folder in `Turbo:Plugin:DevPluginPaths`). On the admin panel's furniture page give a 1x1
floor furni the logic `room_window`; bought from the catalogue it comes as a pair. Place one half in
each room, each facing into its room, near a wall the other room can be drawn behind.

`Turbo.Tests` compiles and loads this folder as a hotel does (`RoomWindowExampleTests`, which also
runs the window logic in a live room) and runs `tests/` with it.
