---
title: "Live reload of a theme's changes"
date: 2026-10-27
pr: turbo-cloud#458, nitro-next#251
breaks: no
who: "theme (data plugin) authors."
---

**What**: a change to a theme (saved in the panel, or a data plugin's file edited on disk) is sent
to the players whose look it is part of, as it is saved (`turbo.theme.changed`), and the client
draws it in place.

**Action**: none; `docs/plugin-data.md`, "Live reload".
