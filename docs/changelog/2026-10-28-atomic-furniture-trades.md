---
title: "Atomic furniture exchanges with recoverable inventory delivery"
date: 2026-10-28
pr: turbo-cloud#494
breaks: no
who: "plugins using inventory grains or observing trade movements."
---

**What**: Confirmed furniture trades commit both offers and their movements together, with a
durable request-bound receipt and retryable inventory reconciliation. `IInventoryGrain` adds
`CommitFurnitureTradeAsync` and `ReconcileFurnitureTradesAsync`; existing transfer methods remain.
A refused exchange produces no half-trade or compensating movements. Delayed receives invalidate
the furniture list and reload current custody instead of installing historical snapshots.
Present opening shares the wrapper lock and rechecks ownership before releasing its contents.

**Action**: none for existing callers. Callers using the new exchange method must retain the same
frozen `FurnitureTradeRequest` and `MovementCause` across ambiguous failures. See `docs/movements.md`,
"Trades". An additive database migration creates the trade receipts table.
