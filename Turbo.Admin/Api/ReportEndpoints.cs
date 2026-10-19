using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The calls for help, for staff with <c>moderation.tool</c>, the node the client's mod tool
/// needs: list and read them, pick one, let it go, and close it. Picking and closing are the mod
/// tool's own (<see cref="IModerationIssueService"/>), as the staff member, so moderators in the
/// hotel see it at once and the reporter is told it was closed. A report another moderator holds
/// can't be closed here either.
/// </summary>
internal sealed class ReportEndpoints(
    IGrainFactory grainFactory,
    AdminReportQueries reports,
    IModerationIssueService issues
)
{
    private const string NO_ACCESS = "You can't handle reports.";
    private const string NOT_FOUND = "There is no such report.";
    private const string CLOSED = "That report is closed.";
    private const string NOT_YOURS = "Only the moderator who picked a report can let it go.";
    private const string BAD_REASON = "Close a report as resolved, useless or abusive.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/reports").AddEndpointFilter(RequireToolAsync);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapPost("/{id:int}/pick", PickAsync);
        group.MapPost("/{id:int}/release", ReleaseAsync);
        group.MapPost("/{id:int}/close", CloseAsync);
    }

    private async Task<IResult> ListAsync(
        string? state,
        string? player,
        int? before,
        int? after,
        CancellationToken ct
    ) =>
        Results.Ok(await reports.ListAsync(state, player, before, after, ct).ConfigureAwait(false));

    private async Task<IResult> GetAsync(int id, CancellationToken ct) =>
        await reports.GetAsync(id, ct).ConfigureAwait(false) is { } report
            ? Results.Ok(report)
            : AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);

    private async Task<IResult> PickAsync(int id, HttpContext http, CancellationToken ct)
    {
        if (await reports.GetAsync(id, ct).ConfigureAwait(false) is not { } report)
            return AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);

        if (await TakeAsync(http, [report.Report], ct).ConfigureAwait(false) is { } refused)
            return refused;

        return Results.Ok(await reports.GetAsync(id, ct).ConfigureAwait(false));
    }

    private async Task<IResult> ReleaseAsync(int id, HttpContext http, CancellationToken ct)
    {
        if (await reports.GetAsync(id, ct).ConfigureAwait(false) is not { } report)
            return AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);

        if (report.Report.State == AdminReportQueries.CLOSED)
            return AdminResults.Error(StatusCodes.Status409Conflict, CLOSED);

        if (report.Report.PickerId != AdminIdentity.Of(http).PlayerId.Value)
            return AdminResults.Error(StatusCodes.Status409Conflict, NOT_YOURS);

        await issues.ReleaseAsync(AdminIdentity.Of(http).PlayerId, [id], ct).ConfigureAwait(false);

        return Results.Ok(await reports.GetAsync(id, ct).ConfigureAwait(false));
    }

    private async Task<IResult> CloseAsync(
        int id,
        CloseReportRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        CfhCloseReasonType? reason = request.Reason switch
        {
            "resolved" => CfhCloseReasonType.Resolved,
            "useless" => CfhCloseReasonType.Useless,
            "abusive" => CfhCloseReasonType.Abusive,
            _ => null,
        };

        if (reason is not { } closeAs)
            return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_REASON);

        if (await reports.GetAsync(id, ct).ConfigureAwait(false) is not { } report)
            return AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);

        // The report first: the mod tool records the sanction on the first of a bundle.
        ReportItem[] closing = [report.Report, .. request.WithRelated ? report.Related : []];
        ImmutableArray<int> ids = [.. closing.Select(x => x.Id)];

        if (await TakeAsync(http, closing, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var moderator = AdminIdentity.Of(http).PlayerId;

        if (request.Sanction && closeAs == CfhCloseReasonType.Resolved)
            await issues.CloseWithDefaultSanctionAsync(moderator, ids, ct).ConfigureAwait(false);
        else
            await issues.CloseAsync(moderator, ids, closeAs, ct).ConfigureAwait(false);

        return Results.Ok(await reports.GetAsync(id, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Picks the reports for the staff member, those they hold already staying theirs; the
    /// refusal when one is closed or another moderator holds it, and then none is picked.
    /// </summary>
    private async Task<IResult?> TakeAsync(
        HttpContext http,
        ReportItem[] reportsToTake,
        CancellationToken ct
    )
    {
        var moderator = AdminIdentity.Of(http).PlayerId;
        ImmutableArray<int> ids = [.. reportsToTake.Select(x => x.Id)];
        var failures = await issues.PickAsync(moderator, ids, ct).ConfigureAwait(false);

        if (failures.IsEmpty)
            return null;

        // All or none: what this call picked goes back, what was theirs before stays.
        var kept = failures
            .Select(x => x.IssueId)
            .Concat(reportsToTake.Where(x => x.PickerId == moderator.Value).Select(x => x.Id))
            .ToHashSet();
        var picked = ids.Where(x => !kept.Contains(x)).ToImmutableArray();

        if (!picked.IsEmpty)
            await issues.ReleaseAsync(moderator, picked, ct).ConfigureAwait(false);

        var failure = failures[0];

        return AdminResults.Error(
            StatusCodes.Status409Conflict,
            failure.PickerUserId > 0
                    ? $"{(failure.PickerUserName.Length > 0 ? failure.PickerUserName : $"Moderator #{failure.PickerUserId}")} has picked report #{failure.IssueId}."
                : failure.IssueId == ids[0] ? CLOSED
                : $"Report #{failure.IssueId} is closed."
        );
    }

    private async ValueTask<object?> RequireToolAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Moderation.TOOL,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
