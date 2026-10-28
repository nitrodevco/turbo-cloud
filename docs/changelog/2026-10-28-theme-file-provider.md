---
title: "Theme-file provider capability"
date: 2026-10-28
pr: turbo-cloud#486
breaks: no
who: "host integrations replacing the content file provider."
---

**What**: `IThemeFileService` extends `IGamedataFileService` with theme variables, revisioned
looks and invalidation notifications. Audience selection and live reload consume that capability
without casting to the built-in implementation. The default base interface resolves the same
singleton, so file reads and theme invalidation share one provider.

**Action**: none for existing plugins. A host replacing theme-file behavior registers its
`IThemeFileService` implementation after the content module; the base file-service alias follows
it. Replacing only `IGamedataFileService` does not replace theme-file behavior. See
`docs/plugin-data.md`, "Replacing the theme-file provider".
