# Room papers, posters, badge displays and song discs

Furni whose catalog offer decides what the item is, rather than its definition alone: one
`wallpaper` definition serves every wallpaper, one `poster` every poster. This page is how the
server keeps what each item is, how a room paper changes a room, and what a hotel sets up.

## What it does

- **What the item is lives on the item.** A wallpaper, floor, landscape, poster (furnidata
  categories 2, 3, 4, 6) or song disc (category 8) is given its **product's** extra parameter
  when it is bought: a pattern (`101`, `1.1`), a poster id (`12`) or a song id (`5`), written as
  the item's legacy stuff data (`{"stuff":{"Data":"12"}}`). The client's own purchase parameter
  is ignored for these. `ProductStuffData` (`Turbo.Primitives/Furniture`) is the one place that
  knows which categories carry one and reads it back (`TryGetValue`, `TryGetSongId`).
- The room sends a wall item's legacy data as its data string, which the client turns into
  the poster asset (`poster` + `12`). The inventory and the trade window send the same data, so
  the item reads the same wherever it is; nothing about it changes on pick-up, placement or trade.
- **A room paper is applied, never placed.** The client sends `RequestRoomPropertySet` with the
  item id when a paper is used from the inventory (and after buying one while in a room).
  `IRoomGrain.ApplyDecorationAsync` lets only the room's owner (or staff who control every room)
  do it, takes the item out of the inventory (`IInventoryGrain.ConsumeFurnitureAsync` deletes its
  row), saves the pattern on the room (`rooms.paint_wall`, `paint_floor`, `paint_landscape`) and
  sends everyone in the room `RoomProperty` (`wallpaper`, `floor`, `landscape`). If the save
  fails, the item is given back. Anyone else, or any other furni, changes nothing.
- **A badge display shows a badge its buyer owns.** The badge display catalog page sends the
  picked badge code as the purchase's extra parameter. The purchase is refused before the buyer is
  charged unless they own that badge. The item is written as the string array the client reads:
  `["0", badgeCode, buyerName, dd-MM-yyyy]`. The `badge_display` logic
  (`FurnitureBadgeDisplayLogic`) reads it as a string array; the client draws index 1 and the
  engraving shows 1 to 3. The inventory works out an item's stuff data type from the stored data
  itself, so the display keeps its badge there too, after a reload as well as after a pick-up.

## Setting it up

- Offers: a wallpaper, floor, landscape, poster or song disc product needs its extra parameter
  (the pattern, the poster id, or the song id as a whole number above zero). An offer without one
  is refused at purchase and logged as an error, because the item would show nothing.
- A badge display offer needs nothing extra; it sits on a `badge_display` layout page, which
  makes the client ask for a badge before it can be bought.
- The migration `MapBadgeDisplayLogic` points `badge_display*` definitions (including
  `badge_display_case`) that still use `default_floor` at the `badge_display` logic. Room papers
  and posters need no logic of their own: the client and the server both go by category.

## Not here

- Achievements for decorating (`RoomDecoWallpaper`, `RoomDecoFloor`, `RoomDecoLandscape`) are not
  fed yet.
- A limited edition poster or paper is granted without its product's id
  (`InventoryFurniModule.GrantLimitedAsync` writes only the serial).
