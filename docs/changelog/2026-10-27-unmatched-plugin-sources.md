---
title: "Reject plugins whose Sources miss their code"
date: 2026-10-27
pr: turbo-cloud#481
breaks: yes
who: "plugin authors whose manifest Sources match no files while the folder contains code"
---

**What**: A plugin whose `Sources` match no `.cs` files is rejected when its folder contains C#
sources or a `.csproj` outside `bin/` and `obj/`. Its error names the globs and where the code is.
Previously it loaded as a data plugin without its commands or handlers. Genuine data plugins
continue to load.

**Breaks plugins**: A code plugin with unmatched `Sources` now reports Rejected instead of Loaded.

**Action**: Move the plugin's sources under `server/`, or set `Sources` to their actual location.
