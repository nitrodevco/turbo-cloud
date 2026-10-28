---
title: "Safe theme creation publication"
date: 2026-10-28
pr: turbo-cloud#489
breaks: no
who: "theme authors and integrations creating themes or installing packages."
---

**What**: New and duplicated themes are built outside plugin discovery and published as complete
directories. Creation and package installation share a case-insensitive key reservation, and
creation rechecks for newly appeared plugins before publication. Failed loads retract only their
own marked output; cleanup errors are logged without replacing the original failure.

**Action**: no API changes. Retry after a conflicting creation or installation finishes. See
`docs/plugin-data.md`, "Themes made in the panel, and where edits land", for scope and limitations.
