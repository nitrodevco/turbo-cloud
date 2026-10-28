---
title: "Theme audience resolution and client tracking"
date: 2026-10-28
pr: turbo-cloud#488
breaks: yes
who: "host integrations implementing IThemeAudiences."
---

**What**: `IThemeAudiences` requires the narrow `IThemeResolver` batch capability. Audience
resolution receives preview requests explicitly. Session initialization and live reload use one
shared `IThemeClientState` for accepted previews and displayed revisions; live reload no longer
depends on the built-in audience implementation.

**Breaks plugins**: implementations of `IThemeAudiences` must implement `ResolveAsync` and record
session revisions in the host's tracking service. Callers of the existing audience API are unchanged.

**Action**: implement batch resolution and inject the shared tracking singleton as described in
`docs/upgrading.md`, "Theme audience providers (2026-10-28)".
