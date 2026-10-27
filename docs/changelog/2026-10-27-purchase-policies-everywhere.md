---
title: "Every way a purchase is made asks the purchase policies, and a failing policy refuses"
date: 2026-10-27
pr: turbo-cloud#459
breaks: yes
who: "plugins with a purchase policy or listening to purchases; tests that build either record."
---

**What**: `PurchaseRequest` gains `Kind` (`Catalog`, `ClubMembership`, `Gift`, `RoomAd`,
`ClubGiftClaim`, `LimitedEdition`) and `ExtraParam`; club gift claims and limited edition entries
now ask the policies too. `PurchaseDecision.Refuse(reason, error)` can show one of the hotel's
purchase errors. A policy that throws now **refuses** the purchase (it was passed over).
`PurchaseCompletedEvent` gains `Kind` and `ItemIds`.

**Breaks plugins**: A policy that throws now blocks purchases, it may now be asked for club
gifts and limited editions, and `Kind` is required where the records are built.

**Action**: make a policy that should not apply to a kind return `proposed` for it; pass `Kind` in
tests. `docs/extensions.md`, "Deny or react to a purchase"; `docs/plugin-policies.md`.
