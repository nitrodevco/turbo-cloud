# Adjacent water furni never join into one pool

The water furni `bw_water_1`, `bw_water_2`, `val13_water` and `stackable_water` are meant to
join seamlessly when placed next to each other: the client draws a shoreline on every edge
of a water tile *unless* the server tells it that a matching water tile is on that side.
Turbo never tells it, so every water tile always shows a full border and pools look like a
grid of separate puddles. (In the database these four definitions use the plain
`default_floor` logic.)

**What the client reads.** For a water item, the item's state (its legacy stuff-data
integer) is a neighbour mask: one bit per cell of the ring of tiles surrounding the item's
footprint. Using coordinates relative to the footprint's origin tile, with the footprint's
width `w` and length `l` *after rotation*, the bits are numbered in this order:

1. the row below the footprint (`y = l`), from `x = w` down to `x = -1`;
2. then each row of the footprint from `y = l - 1` down to `y = 0`: first the cell to the right
   (`x = w`), then the cell to the left (`x = -1`);
3. then the row above the footprint (`y = -1`), from `x = w` down to `x = -1`.

So a 1x1 item has 8 bits (bit 3 = the tile to its east, bit 4 = west, bit 1 = south,
bit 6 = north), and a 2x2 item has 12. A bit is set when that ring cell holds a water item of
the **same furniture definition at the same height**. Different water types keep their
border artwork between them (that is what draws the shallow/deep transition).

Expected:
- The masks are correct whenever water is placed, moved (both the old and the new
  neighbourhood), re-stacked to another height or picked up, for the item itself and for its
  neighbours.
- Players entering the room later receive the current masks with the room's items.
- The mask is live room state: it must not overwrite the item's stored data or cause
  database writes, and an item that is picked up goes back to its original state.
- Other furni and the behaviour of these items otherwise (walking, stacking) are unchanged.
