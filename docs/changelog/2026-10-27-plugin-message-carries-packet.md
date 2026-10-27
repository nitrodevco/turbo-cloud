---
title: "A plugin message can carry a whole core packet"
date: 2026-10-27
pr: turbo-cloud#467, nitro-next#254
breaks: no
who: "plugins relaying another room (portals, screens); client modules reading them."
---

**What**: a plugin message record may end with an `IComposer` field (schema type `"packet"`), a
core packet sent after its other fields as a `PluginEnvelopeComposer`. Server to client only: a
handler for such a message fails the plugin's load. In the client, the field reads as a
`ClientModuleCarriedPacket` (`{ header, packet }`, parsed by the client's own parser) that a
secondary room's `apply` takes as it is; `api.messages.send` refuses such a message. A room
watch's envelope message must now have that field.

**Breaks plugins**: A watch envelope without the field, possible for an hour after
turbo-cloud#464, is now refused with `ArgumentException`.

**Action**: none; to relay, see `docs/plugin-messages.md`, "Carrying a core packet".
