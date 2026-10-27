---
title: "Test kit: approvals"
date: 2026-10-27
pr: turbo-cloud#480
breaks: no
who: "plugins that ask for approval or carry approved requests out, and their tests."
---

**What**: `hotel.Approvals` plays the admin panel's Approvals inbox over the server's own
approvals: the requests a plugin asked for are listed (`PendingAsync`), approved or denied as a
staff member holding their node (`ApproveAsync`, `DenyAsync`; the plugin's `ApprovalHandler<TArgs>`
runs as on a server, refusals as a server gives them), and expire as the clock moves past them,
the plugin's `ApprovalDecidedEvent` handlers hearing of it. Assertions:
`hotel.Approvals.Should().HavePending(kind)`, `NotHavePending()`, `HaveBeen(kind, state)`.

**Action**: none; a plugin's tests can drop their own approval plumbing (`docs/plugin-testing.md`,
"Approvals").
