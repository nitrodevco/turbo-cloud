# Rotating the bottom item of a stack leaves it underneath

In the hotel, rotating an item that sits at the bottom of a stack moves it to the top of the
stack, so players can keep "rotating items upward" to rearrange a pile. In Turbo the rotated
item keeps its old height and stays underneath the items that were on it.

The Flash client only sends x, y and the new direction for a rotate; it takes the item's height
from the server's update, so the height has to be decided on the server.

Expected:
- An item turned in place (same tile, new direction) with no explicit height lands on top of
  whatever else stands on its tiles, as in the hotel. Rotating it again keeps it climbing.
- An item rotated in place with nothing on top of it stays where it is.
- Normal moves to another tile behave as they do today, an explicit height is still respected,
  and a "move" that changes nothing keeps the item's height.
- Wired effects that move/rotate furni without an explicit height go through the same rules.
