# Client capabilities: Turbo's protocol extensions

Turbo speaks Habbo's protocol to any client, and nitro-next speaks it to any emulator. Some
things the Habbo protocol cannot say; for those Turbo offers **extensions**, which a client asks
for by name. A client that never asks (the Flash client, any other port) never sends or receives
an extension packet, so neither side depends on the other.

## The exchange

After `AuthenticationOK` the client may send:

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| client → server | 30000 | `TurboClientCapabilitiesMessage` | `int count`, then per extension `string name`, `int version` |
| server → client | 30000 | `TurboServerCapabilitiesMessage` | the same shape: each extension accepted, at the version both will use |

The server accepts each extension it speaks, at the lower of the two versions
(`ClientCapabilities.Negotiate`), ignores names it does not know, and answers even when it accepts
none. Acceptance belongs to the session: the presence grain keeps it, and clears it when a session
attaches or leaves. An extension's composers implement `ICapabilityComposer`, naming their
extension, and the presence drops one bound for a session that did not accept it. The grains that
compose them send unconditionally and never ask what the client speaks. A server that does not know header 30000 logs and ignores it, and never
answers: the client keeps behaving as a plain Habbo client.

Headers 30000–30099 are reserved for these extensions, in every revision. Habbo's own ids stop at
4101 in revision 20260909; the gap is left so Habbo can grow without meeting them.

## `permission.nodes` (version 1)

| Direction | Header | Packet | Body |
| --- | --- | --- | --- |
| server → client | 30001 | `TurboPermissionNodesMessage` | `int count`, then `string node` per node: the whole set |

Sent straight after the capability answer, then whenever the player's client-facing nodes change,
after `UserRights`/`PerkAllowances` when those changed too. Each of the three goes only when what it
says changed: a node the level already covered sends the node list alone. It carries every node the player holds that a client
gates on: every node with a `ClientLevel` (the Flash `hasSecurity` threshold of its gate), and any
node a plugin registered with `ClientVisible: true`. Server-only nodes (`room.enter.full`,
`chat.speak`), perks and membership nodes are not sent; perks travel in `PerkAllowances` as ever.

A client with the list gates each feature on its node. Without it, it gates on the level, as Flash
does. The level is still sent and still derived from the nodes (`docs/permissions.md` §8), so the
two agree whichever a client reads.

The node names are part of this extension's contract: renaming a node that has a `ClientLevel` is a
protocol change, and nitro-next's gate table (`packages/nitro-react/src/context/user/gates/ClientGate.ts`)
names the same strings.
