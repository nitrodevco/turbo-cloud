---
title: "One theme build for session variables and revisions"
date: 2026-10-28
pr: turbo-cloud#491
breaks: yes
who: "host integrations implementing IThemeFileService or initializing custom theme sessions."
---

**What**: `ThemeViewAsync` returns session variables and the revisioned look from one materialization.
The built-in provider shares one build cache across the combined and legacy views; default session
initialization consumes the pair once. Variables-only reads preserve existing retention behavior.

**Breaks plugins**: `IThemeFileService` implementations must implement the new combined method.
Custom audience providers must consume that combined result when initializing sessions and tracking
their revisions. Existing callers of the legacy projection methods need no source changes.

**Action**: follow `docs/upgrading.md`, "Theme-file providers (2026-10-28)". Return both projections
from one build; preserve default nulls, text URL placement, revision hashing and retained-look lookup.
