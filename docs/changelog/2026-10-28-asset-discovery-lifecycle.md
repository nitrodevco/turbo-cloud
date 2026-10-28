---
title: "Coordinated asset discovery snapshots and refreshes"
date: 2026-10-28
pr: turbo-cloud#492
breaks: no
who: "integrations requesting asset previews or starting asset sync jobs."
---

**What**: Preview refreshes and sync discovery share one catalog owner. Each listing keeps its own
client release with its room-library hashes; overlapping work cannot replace another listing's
release reference. Cached previews remain immediate, stale previews refresh in the background,
and failed or cancelled listings preserve the last successful snapshot. Shutdown drains discovery.

**Action**: none; the public API is unchanged. Cold or explicit refresh requests can wait for active
sync discovery. See `docs/asset-bundles.md`, "Syncing from Habbo".
