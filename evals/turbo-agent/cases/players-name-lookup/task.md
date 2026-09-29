# Looking up the name of one deleted player returns an empty name instead of nothing

The player directory's batch name lookup (player ids in, id → name out) is used for room
rights lists, ban lists, and furni/pet/bot owner names. For ids that don't belong to any player
(for example a deleted account), a lookup of several ids simply leaves them out, and callers
rely on that to skip them. But when the lookup is asked for exactly **one** id, an unknown
player comes back with an empty name. So a room whose only banned player or only rights
holder was deleted shows a blank entry, while rooms with two or more such entries don't.

A lookup should give the same answer however many ids it is asked for: every existing player
with their name, unknown ids left out, duplicates answered once.
