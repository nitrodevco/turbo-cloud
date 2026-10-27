---
title: "Human approval: actions that wait for a staff member's yes"
date: 2026-10-27
pr: turbo-cloud#475
breaks: no
who: "plugins and AI agents whose actions a person should approve; MCP tools that change the hotel; the MCP server plugin (a new call status, not an error)."
---

**What**: `IApprovals` (`Turbo.Contracts.Approvals`) saves a request for an action (kind,
summary, arguments, who asked, the node an approver needs, an expiry); a staff member holding the
node approves or denies it in the panel's new Approvals inbox, never their own; on approval the
`ApprovalHandler<TArgs>` of its kind (a public class in a plugin, found as it loads) carries it
out once. `ApprovalRequestedEvent`, `ApprovalDecidedEvent` and `ApprovalExecutedEvent` tell of
it. An MCP tool with `RequiresApproval = true` answers `McpToolCallStatus.PendingApproval` with the
request's id and runs on approval. New nodes `admin.approvals.view` and `approvals.decide`.

**Action**: none; to use it, read `docs/approvals.md`.
