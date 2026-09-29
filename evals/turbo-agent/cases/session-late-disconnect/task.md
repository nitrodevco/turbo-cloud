# Reconnecting players get logged out when their old connection finally closes

When a player reconnects (for example after a network blip or reloading the client), the new
connection logs in and becomes the player's session. The old socket often only closes a
little later. When that late disconnect is processed, Turbo tears down the player's presence
routing, so the *new* connection stops receiving anything: the player looks online but is
frozen, and messages, room updates etc. go nowhere.

A disconnect must only affect the connection it belongs to. Once a replacement connection
is registered for the player, the old connection closing late must leave the replacement
fully working. A player whose only (or current) connection disconnects must still be
cleaned up as today.
