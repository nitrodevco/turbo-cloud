# Songs, song disks and jukeboxes

A **song** is a trax track the client plays. A **song disk** is a furni carrying one song. A
**jukebox** holds disks in a playlist and plays them in turn for the whole room. This page is how
the server keeps each, what a hotel sets up, and what this client cannot do, so the server does
not either.

The client's side is the jukebox's playlist editor (its owner's double-click), the song disk info
stand, the trade window's disk names, and the catalog's song disk page (layout `soundmachine`).
Everything here was read from the Flash client (`HabboMusicController`,
`JukeboxPlayListController`, `SoundMachinePlayListController`, `PlayListEditorWidgetHandler`,
`FurnitureJukeboxLogic`, `FurnitureSongDiskLogic`, `SongDiskProductViewCatalogWidget` and the
`sound` parsers); Nitro React mirrors it.

## Songs

`songs` (migration `AddSongs`), owned by `SongDirectoryGrain` (`ISongDirectoryGrain`,
`Turbo.Furniture/Grains`), one grain for the hotel:

| Column | Meaning |
|---|---|
| `name`, `author` | what the music widgets show |
| `track` | the trax track in the client's format; the server only stores and sends it |
| `length_seconds` | how long it plays; the client is sent milliseconds |
| `code` | optional, unique: the catalog code an official song can be named by (`GetOfficialSongId`) |
| `is_official` | added by staff to be sold on disks |

The grain loads every song on activation and answers from memory, interleaved: clients ask for
songs (`GetSongInfo`, batched once a second) whenever they meet a disk, a playlist or a catalog
page, and jukeboxes ask for lengths. Changes are written through. Staff manage songs in the admin
panel ([admin-panel.md](admin-panel.md#songs)); a song any disk carries cannot be deleted.

## Song disks

- A song disk is a furni of the **trax song category** (8), which is how the client tells one
  too; the stock `song_disk` definition is one. Its song id is its **legacy stuff data**, written
  when it is bought from the offer product's extra parameter (`ProductStuffData`), so a disk
  carries its song through inventories, trades, rooms and jukeboxes.
- The client never reads that stuff data. It reads the song id from the number written **beside**
  the item: `FurnitureItemSnapshot.Extra` in the inventory and trade lists, and the floor object's
  extras (`RoomFloorItemSnapshot.Extra`, from `IFurnitureFloorLogic.GetObjectExtra`) in a room,
  where a disk has logic `song_disk` (`FurnitureSongDiskLogic`). `SongDisks` says what a disk is
  and reads its song.
- `GetUserSongDisks` lists the disks in the player's inventory (disk id and song) for the
  playlist editor; the editor asks again whenever the inventory changes.

**A catalog song disk offer** gives the `song_disk` definition with the song's **id** as the
product's extra parameter. The song disk page plays a preview from the same parameter (and reads a
parameter that is not a number as an official song's code); only an id puts a song on the disk.

## Jukeboxes

- Logic `jukebox` (`FurnitureJukeboxLogic`). Its disks are `JukeboxGrain`'s (`IJukeboxGrain`,
  keyed by the jukebox's item id, write-through). A disk put in stays its own `furniture` row,
  **still owned by whoever put it in**, held by the jukebox through the column a wired chest uses
  (`chest_item_id`, in no inventory, so every inventory query already leaves it out) and numbered
  by `held_position` in playing order. Taking it out gives it back to that owner. Picking up,
  moving or trading the jukebox keeps its playlist; if its row is ever deleted, the database lets
  the disks go and they are back in their owners' inventories.
- The packets name no item: the client keeps **one music player per room**, so the room hands
  them to its first jukebox or sound machine (`IRoomGrain.InteractWithMusicPlayerAsync`).
- **Who may do what** follows the client. Only the jukebox's owner gets the playlist editor, so
  only they add (`AddJukeboxDisk`) and take out (`RemoveJukeboxDisk`) disks; a full playlist
  answers `JukeboxPlayListFull` (`RoomConfig.JukeboxMaxDisks`, 10, also sent with every playlist).
  A disk must be in the owner's inventory and carry a song the hotel has. The owner and anyone
  with rights switch it on and off with a use: the editor's play button sends the selected index,
  a controller's double-click `-2` (which starts at the top).
- **Playing.** The client plays each song itself and never says when one ends, so the room keeps
  the clock: which disk plays and since when. It tells the room `NowPlaying` (song, index, next
  song, next index, milliseconds in) when a song starts, and moves on when the song's length is
  up (`RoomTimerSystem`), round the playlist, until switched off (`NowPlaying` of all `-1`). A
  player who enters asks `GetNowPlaying` and joins the song where the room is. Every change to the
  playlist is sent to the room (`JukeboxSongDisks`), and taking out the disk that plays starts the
  one after it. The state is `JukeboxStates.ON` while it plays; a jukebox playing when its room
  unloaded starts its playlist over when it loads.

## Trax machines

Logic `sound_machine` (`FurnitureSoundMachineLogic`). **This client can only play one.** There is
no trax editor in it (no composing, no sound sets loaded into a machine, no saving and no burning
to a disk; Habbo removed the editor with Shockwave), and nothing opens a playlist editor for a
machine. So the server implements no composing or burning, and `sound_set_*` furni (category 7)
keep plain logic. Switched on, the client asks `GetSoundMachinePlayList` and plays the list round
by itself from the point it is told (`PlayList`). The machine shares the jukebox's playlist and
clock, so it plays whatever disks its grain holds: none, since this client cannot put any in.

## Setting up

- Apply the migration `AddSongs`: it adds `songs`, `furniture.held_position`, and maps the stock
  definitions (`%jukebox%` to `jukebox`; `sound_machine*`, `nouvelle_trax` and `ads_idol_trax` to
  `sound_machine`; every category 8 furni to `song_disk`). `traxbronze`, `traxsilver` and
  `traxgold` are left alone.
- Add the songs in the admin panel, then the song disk offers (above).

## Not built

- Composing, saving and burning trax songs, and sound sets: the client has none of it.
- One jukebox per room is not enforced at placement; a second one plays nothing (the room uses
  its first).
- A disk handed back from a jukebox is marked new in the inventory, as any received item is.
