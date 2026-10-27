# Human approval

- [Introduction](#introduction)
- [Asking for approval](#asking-for-approval)
    - [Who asked](#who-asked)
    - [Arguments](#arguments)
- [Carrying it out](#carrying-it-out)
- [Deciding](#deciding)
- [Expiry](#expiry)
- [Events](#events)
- [MCP tools that need approval](#mcp-tools-that-need-approval)
- [AI agents](#ai-agents)
- [The Approvals inbox](#the-approvals-inbox)
- [Testing](#testing)
- [What it changed in the server](#what-it-changed-in-the-server)

## Introduction

Some actions should not change the hotel until a person says yes: an AI assistant refunding
credits, an agent kicking a player, a plugin handing out a rare, a big refund a staff member asks
for. Approvals are core's building block for that. Whoever wants the action asks for it; the
request is saved and waits; a staff member holding the right permission node approves or denies it
in the admin panel; on approval the handler registered for its kind carries it out, once.

What the server promises:

- **Nothing runs before a yes.** Asking saves the request and answers its id. The action is the
  handler's, and the handler runs only after an approval.
- **At most once.** Approving claims the request in one conditional update of its row (pending and
  not expired, to approved). Two staff pressing at the same moment, on one server or two, get one
  run; the other is told it was approved already.
- **Nobody decides their own.** The player a request was asked by or for can neither approve nor
  deny it.
- **It survives a restart.** Requests live in the `approvals` table. A request asked before a
  restart is approved after it as any other, by the handler the server has now.
- **It is on record.** The row keeps who asked, what, who decided, when, their note, and what
  carrying it out answered. The panel's decisions also go to the command log.

## Asking for approval

Take `IApprovals` (`Turbo.Contracts.Approvals`) by constructor. In a plugin it is bound to the
plugin, so what it asks is recorded as the plugin's own:

```csharp
using Turbo.Contracts.Approvals;

public sealed class RefundDesk(IApprovals approvals)
{
    public async Task<int> AskAsync(PlayerId staff, PlayerId player, int credits, CancellationToken ct)
    {
        var approval = await approvals.RequestAsync(
            new ApprovalRequest
            {
                Kind = "shop.refund",
                Summary = $"Refund player {player} {credits:N0} credits",
                Arguments = new RefundArgs(player.Value, credits),
                Requester = ApprovalRequester.Staff(staff),
                Node = "plugin.shop.refund.approve",
                ExpiresIn = TimeSpan.FromHours(4),
            },
            ct
        );

        return approval.Id;
    }
}

public sealed record RefundArgs(int Player, int Credits);
```

| Property | Required | Means |
| --- | --- | --- |
| `Kind` | yes | which handler carries it out: lower-case letters, digits, `.`, `_`, `-` and `:`, at most 64 (`shop.refund`) |
| `Summary` | yes | the sentence the approver reads, at most 500 characters |
| `Arguments` | no | what the handler is given: a record or class (written in camelCase), or a JSON object; at most 16,000 characters as JSON |
| `Requester` | yes | who or what asked (below) |
| `Node` | no | the node an approver must hold; `approvals.decide` by default. Give each kind of action its own, so staff are trusted with them one by one |
| `ExpiresIn` | no | how long it waits; 24 hours by default, from a minute to 30 days |

`RequestAsync` answers the saved `Approval` (its `Id`, `State` `Pending`, `ExpiresAtUtc`, ...).
A request it can't save throws `ArgumentException` with a sentence saying why. `GetAsync(id)`,
`ListAsync(query)` and `CountPendingAsync()` read requests back.

### Who asked

`ApprovalRequester` says what kind of thing asked, the player it acts for, and a name for one that
is not a player:

| Made by | Kind | Acts for |
| --- | --- | --- |
| `ApprovalRequester.Player(id)` | `Player` | a player, from the game |
| `ApprovalRequester.Staff(id)` | `Staff` | a staff member, from the panel or a command |
| `ApprovalRequester.Agent(name, onBehalfOf?)` | `Agent` | an AI agent, for the player who started its run |
| `ApprovalRequester.Token(owner, name?)` | `Token` | an assistant using a staff member's access token (MCP) |
| `ApprovalRequester.System(name)` | `System` | the server or a plugin on its own |

The player it acts for (`OnBehalfOf`) is the one who can't decide it. A request from an assistant or
an agent is asked for the staff member or player behind it, so they can't approve what their own
assistant asked for.

### Arguments

The arguments are saved as JSON and read back into the handler's type. The approver sees them as
they are saved, as text: a value a player or an assistant wrote is shown, never run or rendered.
Keep them to what the handler needs to do the job (ids and amounts, not whole objects), so the
approver can read them.

## Carrying it out

A public class deriving from `ApprovalHandler<TArgs>` in a plugin's assembly is found as the
plugin loads, built with its services and the host's (as an MCP tool or a web route is), and taken
away as it unloads:

```csharp
public sealed class RefundHandler(IHotel hotel) : ApprovalHandler<RefundArgs>
{
    public override string Kind => "shop.refund";

    public override async Task<ApprovalResult> ExecuteAsync(
        RefundArgs args, Approval approval, CancellationToken ct)
    {
        var paid = await hotel.Wallet.CreditAsync(
            new PlayerId(args.Player), "credits", args.Credits,
            $"refund:{approval.Id}", "refund", ct);

        return paid is WalletOutcome.Done or WalletOutcome.AlreadyDone
            ? ApprovalResult.Done($"Refunded {args.Credits:N0} credits.")
            : ApprovalResult.Failed("The wallet refused the refund.");
    }
}
```

- It runs once per approved request, on the server the approver used, right after the yes. The
  approver waits for it and sees what it answered.
- `approval` says who asked, who approved and their note.
- `ApprovalResult.Done(message?)` or `ApprovalResult.Failed(message)`; the message (at most 1,000
  characters) is kept with the request. A handler that throws leaves the request `Failed` with
  "its error is in the server's log", and the exception is logged with the kind and its owner.
- Use the request's id as the reference of anything that must not happen twice (a wallet credit),
  as above.
- **One kind, one owner.** A plugin whose handler takes a kind core or another live plugin carries
  out fails its load, naming the other.
- When nothing live carries a kind out (its plugin is turned off or failed), approving it is
  refused with "Nothing can carry out ... now" and it stays pending.

Core registers its own handlers as `IApprovalHandler` services.

## Deciding

`ApproveAsync(id, by, note)` and `DenyAsync(id, by, note)` answer an `ApprovalDecision`: its
`Status`, a sentence for the decider, and the request as it now stands.

| Status | Means |
| --- | --- |
| `Executed` | approved and carried out |
| `Failed` | approved, and carrying it out failed: the message says why |
| `Denied` | denied |
| `NotFound` | there is no such request |
| `NotPending` | someone decided it already, or it expired |
| `OwnRequest` | the decider asked for it, or it was asked for them |
| `NotPermitted` | the decider does not hold its node |
| `NoHandler` | nothing live carries its kind out; it stays pending |

A request goes `Pending`, then `Approved` (while its handler runs) and `Executed` or `Failed`; or
`Denied`; or `Expired`. One left `Approved` is one whose server stopped while it ran: it is never
run again.

## Expiry

A pending request past its `ExpiresAtUtc` reads as expired at once, and every server sweeps them
once a minute, so what waits on one hears of it from `ApprovalDecidedEvent` without anyone opening
the panel. An expired request can't be approved.

## Events

Post-commit events (`docs/plugin-policies.md`), each carrying the `Approval` as it then stood:

| Event | Raised |
| --- | --- |
| `ApprovalRequestedEvent` | a request was saved |
| `ApprovalDecidedEvent` | it was approved (before it is carried out), denied, or expired; once per request |
| `ApprovalExecutedEvent` | an approved request was carried out (`Succeeded`) or failed |

## MCP tools that need approval

An MCP tool (`docs/plugin-mcp-tools.md`) that changes the hotel can ask for a person's yes on every
call:

```csharp
[RequiresPermission("plugin.shop.refund")]
public sealed class RefundTool(IHotel hotel) : McpTool<RefundInput>
{
    public override string Name => "refund";
    public override string Description => "Gives a player credits back. A staff member approves each refund.";
    public override bool RequiresApproval => true;
    public override string ApprovalNode => "plugin.shop.refund.approve";

    public override async Task<McpToolResult> HandleAsync(RefundInput input, McpToolContext ctx, CancellationToken ct) { ... }
}
```

Called over MCP, the arguments are checked against the schema as for any tool, and then the tool
does not run. The call answers `McpToolCallStatus.PendingApproval` (not an error):

```
refund was not run yet: it needs a person's approval. Pending approval 42; it runs when a staff
member approves it in the admin panel, and is dropped if nobody does by 2026-10-28 21:00 UTC.
Don't call it again for the same thing.
```

with `{"approvalId": 42, "state": "pending", "expiresAt": "..."}` as its structured content. The
request's kind is `mcp.tool`, its arguments `McpToolApproval(Tool, Arguments)`, the plugin the
tool's owner, and it is asked for the staff member whose token the assistant used. When another
staff member holding `ApprovalNode` approves it, the tool runs for the asking staff member, who must
still hold one of its nodes, with the arguments it was checked with; its answer is the request's
result. A read-only tool can't need approval: it fails the plugin's load.

## AI agents

An agent that pauses for a person asks with `ApprovalRequester.Agent(name, onBehalfOf)`, keeps the
id with its paused run, and registers an `ApprovalHandler` for its kind that does the paused step
and resumes the run. `ApprovalDecidedEvent` with `Denied` or `Expired` is the refusal to resume it
with. The AI SDK (`docs/ai-sdk.md`, Approvals) does this for plugins' agents: a tool call that
changes things is a request of kind `ai.tool`, the plugin's, whose arguments are
`AiToolApproval(ConversationId, Tool, Arguments)`; approving it runs the tool and the run goes on,
and its answer is the request's result.

## The Approvals inbox

Staff with `admin.approvals.view` get **Approvals** in the panel (`docs/admin-panel.md`): the
requests newest first, pending by default, narrowed by state, kind or plugin, with the number
pending by its name. A request's page shows its summary, who asked and for whom, its arguments,
the node it needs, when it expires, and, once decided, who decided, their note and what carrying it
out answered. **Approve** and **Deny** (with a note) are offered on a pending request whose node
you hold and that is not your own. The inbox updates live.

| Route | Does |
| --- | --- |
| `GET /api/approvals?state=&kind=&plugin=&page=` | a page of requests (50), `pending` the count; `state` is `pending`, `approved`, `executed`, `failed`, `denied`, `expired` or `all`; `plugin=core` for core's own |
| `GET /api/approvals/count` | `{ "pending": n }` |
| `GET /api/approvals/{id}` | one request |
| `POST /api/approvals/{id}/approve` `{ "note": "..." }` | approve and carry it out; 403 for your own or without its node, 409 when decided or expired, 503 when nothing carries its kind out |
| `POST /api/approvals/{id}/deny` `{ "note": "..." }` | deny |

Each request in an answer says `canDecide` (pending, not yours, and you hold its node) and `own`.
Every approve and deny, refused ones included, goes to the command log as
`approval approve|deny <id> <kind>`.

| Node | Allows |
| --- | --- |
| `admin.approvals.view` | the Approvals inbox |
| `approvals.decide` | deciding a request that names no node of its own |

## Testing

`HotelApprovals` is the server's implementation; in `Turbo.Tests`, `PluginTestHost.Approvals` is
it over the test's database. `ApprovalTests` (request, approve, deny, expiry, own request, node,
two approvals at once, restart, no handler, a handler that throws), `ApprovalPluginTests` (an MCP
tool's pending call and its approval, a plugin's handler, unloading) and `ApprovalEndpointsTests`
(the inbox) show the shapes.

## What it changed in the server

- `Turbo.Contracts`: `Approvals.IApprovals`, `ApprovalRequest`, `ApprovalRequester`,
  `ApprovalRequesterKind`, `Approval`, `ApprovalState`, `ApprovalQuery`, `ApprovalList`,
  `ApprovalDecision`, `ApprovalDecisionStatus`, `ApprovalJson`, `IApprovalHandler`,
  `ApprovalHandler<TArgs>`, `ApprovalResult`, `ApprovalRequestedEvent`, `ApprovalDecidedEvent`,
  `ApprovalExecutedEvent`; `Plugins.Mcp.IMcpTool.RequiresApproval` and `ApprovalNode`,
  `McpToolCallStatus.PendingApproval`, `McpToolApproval`, `McpToolDefinition.CheckArguments` /
  `RunAsync` / `WriteArguments`.
- `Turbo.Primitives`: `PermissionNodes.Approvals.DECIDE` and `VIEW`,
  `PluginContributionKinds.APPROVAL_HANDLER` (`approval-handler` in the inspector).
- `Turbo.Database`: the `approvals` table (migration `AddApprovals`).
- `Turbo.Plugins`: `HotelApprovals`, `ApprovalHandlers` (the scan, one owner per kind),
  `ApprovalExpiry`, `McpToolApprovalHandler`.
- `Turbo.Admin`: `/api/approvals`, `canViewApprovals` in `/api/me`, `approvals` in the live stream.
