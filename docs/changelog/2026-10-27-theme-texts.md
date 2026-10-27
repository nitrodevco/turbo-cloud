---
title: "A theme's texts draw over the hotel's"
date: 2026-10-27
pr: turbo-cloud#456, nitro-next#247
breaks: yes
who: "theme authors."
---

**What**: a theme's `gamedata/external_texts.txt` is no longer seeded into the hotel's texts. Players
drawing the theme get its texts over the hotel's (`text.overrides`), for every key staff never
changed. Plugins that are not themes are seeded as before.

**Breaks plugins**: For a theme that brought texts of its own: every player had them, now only
the players drawing the theme do.

**Action**: none for a theme; `docs/plugin-data.md`, "A theme's texts".
