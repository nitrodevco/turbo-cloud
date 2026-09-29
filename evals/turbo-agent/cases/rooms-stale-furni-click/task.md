# Clicking furniture that is not (or no longer) in the room is logged as an error

The server logs an error with a `FloorItemNotFound` exception and stack trace whenever a
player clicks a furni the room does not hold. This happens constantly in normal play:

- the client lets a player click its placement preview before the place packet has arrived,
  and
- a player can click an item that another player has just picked up.

Neither is a server fault. Such a click should simply do nothing (and not be reported as an
error), while clicks on items that are in the room must keep working exactly as before.
