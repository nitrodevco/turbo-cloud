---
title: "The documentation has a sidebar and a website"
date: 2026-10-27
pr: turbo-cloud#474
breaks: no
who: "everyone reading the plugin guides."
---

**What**: `docs/documentation.md` orders every page under a sidebar, and each page opens with a
table of contents and an Introduction. New guides: Installation, Configuration, Directory
Structure, Deployment, Packet Lifecycle, Grains & Orleans, Errors & Logging, Furni Logic & Room
Turns, Testing. `docs-site/` builds them, with nitro-next's guides, into a searchable site with
`llms.txt` and a markdown copy of every page.

**Action**: none; start at `docs/documentation.md`.
