# Room heights are wrong: floors load almost flat, the camera starts at the wrong height, walls are fixed

Three related problems with room heights:

1. **Heightmap tiles.** Room models are stored as heightmap text: `x` is a void tile, digits
   `0`-`9` are tile heights 0-9, and letters continue the scale (`a` = 10, `b` = 11, ... `z` =
   35). Turbo loads these far too small: a tile marked `1` ends up at 0.01 instead of 1.0, so
   raised floors and stairs are effectively flat and avatars/furni sit at the wrong height.

2. **Initial camera height.** When a player enters a room, the camera's starting Z is taken
   from the door tile's value in the packed heightmap sent to the client. That value is the
   heightmap packet's own encoding of a tile (a fixed-point height plus flag bits), not a
   plain height, but Turbo uses the packed number as if it were one, so the camera starts at
   a nonsense altitude. It should be decoded the way it was encoded; blocked (negative)
   values should give 0.

3. **Walls.** Rooms that never set an explicit wall height should get automatic walls. The
   client's floor-heightmap packet uses `-1` for "automatic walls above the highest floor";
   `0` means a fixed wall height of zero. Turbo's hotel default currently produces fixed
   walls for every such room.
