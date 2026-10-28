---
title: "Control room bots through the hotel API"
date: 2026-10-28
pr: turbo-cloud#484
breaks: no
who: "plugin authors building room bot behavior or off-turn chat listeners."
---

**What**: `hotel.Bots` reads and acts on room bots through the normal room-owned order
flow. Plugins can inspect floor furniture and catalog offers, and their chat listeners
receive the addressed whisper name and speaker tile without changing whisper delivery.
The test kit supplies matching bot, furniture and offer fakes.

**Action**: use the hotel API from off-turn work rather than reaching into room internals.
Use `PlayerChatEvent.RecipientName`, `SpeakerX` and `SpeakerY` when routing a chat line
to a bot. Existing chat listeners and bot orders continue to work.
