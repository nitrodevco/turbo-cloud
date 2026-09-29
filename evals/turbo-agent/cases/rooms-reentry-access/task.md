# Reloading the room you are already standing in can kick you out

A player whose avatar is already in a room can re-request entry to that same room (for
example by reloading it from the navigator). Turbo runs the full entry checks again as if
they were arriving:

- in a full room they are refused as "room full", although they already hold one of its
  slots, and
- in a locked (doorbell) or password room they are sent back to the doorbell / password
  prompt, although they are already past the door.

Either way they end up ejected from a room they legitimately occupy. A player who is already
inside should be let back in without counting against capacity or the door. Bans must still
apply (a ban placed while they are inside takes effect), and players who are not inside keep
getting today's checks.
