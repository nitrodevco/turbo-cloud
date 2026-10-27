using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Contracts.Approvals;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The Approvals inbox (docs/approvals.md): what plugins, AI agents and MCP tools asked to do and
/// wait for a person's yes, for staff with <c>admin.approvals.view</c>. Approving or denying one
/// needs the node the request names, and nobody decides their own; the hotel's
/// <see cref="IApprovals"/> checks both, and carries an approved one out once. Every decision,
/// refused ones too, goes to the command log.
/// </summary>
internal sealed class ApprovalEndpoints(
    IApprovals approvals,
    IGrainFactory grainFactory,
    AdminActionLog audit
)
{
    private const string NO_ACCESS = "You can't see the approvals inbox.";
    private const string NOT_FOUND = "There is no such approval.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/approvals")
            .RequirePermission(PermissionNodes.Approvals.VIEW, NO_ACCESS)
            .ChecksInHandler(
                "Approving or denying needs each request's own node, and never the decider's own request; the hotel's approvals check both, and the refusal goes to the command log."
            );

        group.MapGet("/", ListAsync);
        group.MapGet("/count", CountAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapPost("/{id:int}/approve", ApproveAsync);
        group.MapPost("/{id:int}/deny", DenyAsync);
    }

    private async Task<IResult> ListAsync(
        string? state,
        string? kind,
        string? plugin,
        int? page,
        HttpContext http,
        CancellationToken ct
    )
    {
        ApprovalState? wanted = null;

        if (!string.IsNullOrEmpty(state) && state != "all")
        {
            if (!Enum.TryParse<ApprovalState>(state, ignoreCase: true, out var parsed))
                return AdminResults.Error(
                    StatusCodes.Status400BadRequest,
                    "A state is pending, approved, executed, failed, denied, expired or all."
                );

            wanted = parsed;
        }

        var list = await approvals
            .ListAsync(
                new ApprovalQuery
                {
                    State = wanted,
                    Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim(),
                    Plugin = string.IsNullOrWhiteSpace(plugin) ? null : plugin.Trim(),
                    Page = page ?? 1,
                    PageSize = PAGE_SIZE,
                },
                ct
            )
            .ConfigureAwait(false);
        var views = await ViewsAsync(http, list.Entries, ct).ConfigureAwait(false);

        return Results.Ok(
            new ApprovalListResponse(
                list.Total,
                Math.Max(1, page ?? 1),
                PAGE_SIZE,
                await approvals.CountPendingAsync(ct).ConfigureAwait(false),
                views
            )
        );
    }

    private async Task<IResult> CountAsync(CancellationToken ct) =>
        Results.Ok(
            new ApprovalCountResponse(await approvals.CountPendingAsync(ct).ConfigureAwait(false))
        );

    private async Task<IResult> GetAsync(int id, HttpContext http, CancellationToken ct)
    {
        if (await approvals.GetAsync(id, ct).ConfigureAwait(false) is not { } approval)
            return AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);

        return Results.Ok((await ViewsAsync(http, [approval], ct).ConfigureAwait(false))[0]);
    }

    private Task<IResult> ApproveAsync(
        int id,
        ApprovalDecisionRequest? body,
        HttpContext http,
        CancellationToken ct
    ) => DecideAsync(id, true, body?.Note, http, ct);

    private Task<IResult> DenyAsync(
        int id,
        ApprovalDecisionRequest? body,
        HttpContext http,
        CancellationToken ct
    ) => DecideAsync(id, false, body?.Note, http, ct);

    private async Task<IResult> DecideAsync(
        int id,
        bool approve,
        string? note,
        HttpContext http,
        CancellationToken ct
    )
    {
        var player = AdminIdentity.Of(http).PlayerId;

        // Carrying it out must not stop half way because the browser went away.
        var decision = approve
            ? await approvals
                .ApproveAsync(id, player, note, CancellationToken.None)
                .ConfigureAwait(false)
            : await approvals
                .DenyAsync(id, player, note, CancellationToken.None)
                .ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.APPROVAL,
                player,
                $"{(approve ? "approve" : "deny")} {id}{(decision.Approval is { } a ? $" {a.Kind}" : string.Empty)}",
                decision.Status switch
                {
                    ApprovalDecisionStatus.Executed or ApprovalDecisionStatus.Denied =>
                        CommandOutcome.Completed,
                    ApprovalDecisionStatus.OwnRequest or ApprovalDecisionStatus.NotPermitted =>
                        CommandOutcome.Refused,
                    _ => CommandOutcome.Failed,
                },
                CancellationToken.None
            )
            .ConfigureAwait(false);

        var status = decision.Status switch
        {
            ApprovalDecisionStatus.Executed
            or ApprovalDecisionStatus.Failed
            or ApprovalDecisionStatus.Denied => StatusCodes.Status200OK,
            ApprovalDecisionStatus.NotFound => StatusCodes.Status404NotFound,
            ApprovalDecisionStatus.OwnRequest or ApprovalDecisionStatus.NotPermitted =>
                StatusCodes.Status403Forbidden,
            ApprovalDecisionStatus.NoHandler => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status409Conflict,
        };

        if (status != StatusCodes.Status200OK)
            return AdminResults.Error(status, decision.Message);

        return Results.Ok(
            new ApprovalDecisionResponse(
                decision.Status == ApprovalDecisionStatus.Executed
                    || decision.Status == ApprovalDecisionStatus.Denied,
                decision.Message,
                (await ViewsAsync(http, [decision.Approval!], ct).ConfigureAwait(false))[0]
            )
        );
    }

    private const int PAGE_SIZE = 50;

    /// <summary>The requests as the inbox shows them: names, and whether this staff member may decide each.</summary>
    private async Task<ImmutableArray<ApprovalView>> ViewsAsync(
        HttpContext http,
        IReadOnlyList<Approval> entries,
        CancellationToken ct
    )
    {
        var me = AdminIdentity.Of(http);
        var held = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (
            var node in entries
                .Where(x => x.State == ApprovalState.Pending)
                .Select(x => x.Node)
                .Distinct()
        )
            held[node] = await me.HoldsAsync(grainFactory, node, ct).ConfigureAwait(false);

        var ids = entries
            .SelectMany(x => new[] { x.Requester.OnBehalfOf, x.DecidedBy })
            .Where(x => x is { Value: > 0 })
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var names =
            ids.Count == 0
                ? ImmutableDictionary<PlayerId, string>.Empty
                : await grainFactory
                    .GetPlayerDirectoryGrain()
                    .GetPlayerNamesAsync(ids, ct)
                    .ConfigureAwait(false)
                    ?? ImmutableDictionary<PlayerId, string>.Empty;

        string? Name(PlayerId? id) =>
            id is { } value && names.TryGetValue(value, out var name) ? name : null;

        return
        [
            .. entries.Select(x => new ApprovalView(
                x.Id,
                x.Kind,
                x.Plugin,
                x.Summary,
                x.Arguments,
                x.Requester.Kind.ToString().ToLowerInvariant(),
                x.Requester.OnBehalfOf?.Value,
                Name(x.Requester.OnBehalfOf),
                x.Requester.Name,
                x.Node,
                x.State.ToString().ToLowerInvariant(),
                x.RequestedAtUtc,
                x.ExpiresAtUtc,
                x.DecidedBy?.Value,
                Name(x.DecidedBy),
                x.DecidedAtUtc,
                x.Note,
                x.ExecutedAtUtc,
                x.Result,
                x.State == ApprovalState.Pending
                    && x.Requester.OnBehalfOf != me.PlayerId
                    && held.GetValueOrDefault(x.Node),
                x.State == ApprovalState.Pending && x.Requester.OnBehalfOf == me.PlayerId
            )),
        ];
    }
}

internal sealed record ApprovalDecisionRequest(string? Note);

internal sealed record ApprovalCountResponse(int Pending);

internal sealed record ApprovalListResponse(
    int Total,
    int Page,
    int PageSize,
    int Pending,
    ImmutableArray<ApprovalView> Entries
);

internal sealed record ApprovalDecisionResponse(bool Ok, string Message, ApprovalView Approval);

/// <summary>
/// One request as the inbox shows it. The arguments are as saved; text a player or an assistant
/// wrote in them is shown as text, never run or rendered.
/// </summary>
internal sealed record ApprovalView(
    int Id,
    string Kind,
    string? Plugin,
    string Summary,
    JsonElement Arguments,
    string RequesterKind,
    int? RequesterId,
    string? RequesterPlayerName,
    string? RequesterName,
    string Node,
    string State,
    DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc,
    int? DecidedById,
    string? DecidedByName,
    DateTime? DecidedAtUtc,
    string? Note,
    DateTime? ExecutedAtUtc,
    string? Result,
    bool CanDecide,
    bool Own
);
