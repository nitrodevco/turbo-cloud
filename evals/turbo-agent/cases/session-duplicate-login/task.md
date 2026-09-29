# Logging in twice leaves the first connection half-alive

If the same account logs in a second time (another tab, another machine), the second login
takes over the player's presence, but the first connection is left open and is still
treated as that player: packets it sends are still handled as coming from the player, and
the first client is never told what happened.

Expected, as in the hotel:
- The old connection is told why it is being disconnected with the client's
  `DisconnectReason` message, reason **2** ("concurrent login"). The client reads the reason
  as a single int; Turbo currently sends that message with no payload at all.
- The old connection is then closed.
- From the moment it is replaced, the old connection no longer acts as the player.
- The new connection is unaffected and keeps receiving everything for the player.
- A first login, or the same connection authenticating again, kicks nobody.
