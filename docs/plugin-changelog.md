# Plugin Changelog

- [Introduction](#introduction)
- [Adding an entry](#adding-an-entry)

## Introduction

Every change that alters what a plugin author writes, sees or gets: the contracts (`Turbo.Contracts`,
`Turbo.Primitives`, `Turbo.Database`), hooks and policies, how plugins load, build and install, the
template, and the client module API (nitro-next). To bring a plugin up to date step by step, read
`docs/upgrading.md`.

Each entry says what changed, who it affects, whether it **breaks plugins** (a plugin that built
and ran before stops compiling, stops loading or behaves differently without a change of its own),
the action to take, and the pull request.

Each entry is a file of its own in [`docs/changelog/`](changelog/), named by its date
(`2026-10-27-test-kit.md`). The documentation site shows them all on this page, newest first under
their date; on GitHub, open the folder and read the newest files.

## Adding an entry

A pull request that changes what a plugin author writes or sees adds one file to `docs/changelog/`,
in the same pull request; the pull request template's "Plugin authors" section asks for it. A file
of its own means two pull requests adding entries never touch the same lines. A client module API
change in nitro-next gets its entry here with the turbo-cloud pull request it goes with, or in a
small docs pull request of its own. A change no plugin can notice (internals, core's own behaviour,
tests) has no entry.

Name the file `<yyyy-mm-dd>-<slug>.md`: the day it merges (today's, when you open the pull request)
and a few lower-case words of your own. It starts with these fields and then says what changed and
what to do:

```markdown
---
title: "Test kit: approvals"
date: 2026-10-27
pr: turbo-cloud#480
breaks: no
who: "plugins with approval handlers and tests."
---

**What**: `hotel.Approvals` lists the pending requests, approves or denies one as a staff
member holding its node (the plugin's handler runs as on a server), and expires them as the
clock moves.

**Action**: none; `docs/plugin-testing.md`, "Approvals".
```

| Field | Says |
| --- | --- |
| `title` | the entry's heading, in quotes |
| `date` | the same date as the file's name |
| `pr` | the pull request, `<repository>#<number>`; several are separated by commas (`turbo-cloud#467, nitro-next#254`) |
| `breaks` | `yes` or `no`: does it break plugins (above) |
| `who` | who it affects, in quotes |

The body is `**What**:` and `**Action**:`, and `**Breaks plugins**:` between them when `breaks` is
`yes` (what stops working) or when a `no` needs a sentence. A breaking change also says what to do
in `docs/upgrading.md`. `node docs-site/scripts/changelog.mjs --check` checks every file as the
pull request's checks do, and the site's build (`docs-site/`) puts them on this page.

<!-- The site's build writes every entry to docs-site/generated/changelog.md and puts it here. -->
<!--@include: ../docs-site/generated/changelog.md-->
