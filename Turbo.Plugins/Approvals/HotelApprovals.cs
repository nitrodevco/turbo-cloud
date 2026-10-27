using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Database.Entities.Approvals;
using Turbo.Primitives.Events;
using Turbo.Primitives.Players;

namespace Turbo.Plugins.Approvals;

/// <summary>
/// The hotel's approvals (<see cref="IApprovals"/>), kept in the <c>approvals</c> table so they
/// survive a restart and are the same on every server. A decision claims its request in one
/// conditional update (pending and not expired, to approved or denied), so of two staff pressing
/// at once one wins and the other is told it was decided; the handler runs after the claim, once.
/// </summary>
internal sealed class HotelApprovals(
    IDbContextFactory<TurboDbContext> dbFactory,
    ApprovalHandlers handlers,
    IServiceProvider host,
    TimeProvider clock,
    ILogger<HotelApprovals> logger
) : IApprovals
{
    private static readonly TimeSpan MIN_EXPIRY = TimeSpan.FromMinutes(1);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<Approval> RequestAsync(ApprovalRequest request, CancellationToken ct) =>
        RequestAsync(request, null, ct);

    /// <summary>Saves <paramref name="request"/> as asked by <paramref name="plugin"/> (null for core).</summary>
    public async Task<Approval> RequestAsync(
        ApprovalRequest request,
        string? plugin,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ApprovalHandlers.IsKind(request.Kind))
            throw new ArgumentException(
                $"An approval's kind is 1 to {ApprovalRequest.KIND_MAX_LENGTH} lower-case letters, digits, '.', '_', '-' or ':'; \"{request.Kind}\" is not.",
                nameof(request)
            );

        if (
            string.IsNullOrWhiteSpace(request.Summary)
            || request.Summary.Length > ApprovalRequest.SUMMARY_MAX_LENGTH
        )
            throw new ArgumentException(
                $"An approval's summary is the sentence its approver reads, 1 to {ApprovalRequest.SUMMARY_MAX_LENGTH} characters.",
                nameof(request)
            );

        if (string.IsNullOrWhiteSpace(request.Node) || request.Node.Length > 128)
            throw new ArgumentException(
                "An approval's node is the permission node its approver must hold.",
                nameof(request)
            );

        var requester =
            request.Requester ?? throw new ArgumentException("An approval says who asked.");

        if (requester.OnBehalfOf is null && string.IsNullOrWhiteSpace(requester.Name))
            throw new ArgumentException(
                "An approval's requester names the player it acts for, or itself.",
                nameof(request)
            );

        if (requester.Name is { Length: > ApprovalRequester.NAME_MAX_LENGTH })
            throw new ArgumentException(
                $"A requester's name is at most {ApprovalRequester.NAME_MAX_LENGTH} characters.",
                nameof(request)
            );

        var expiresIn = request.ExpiresIn ?? ApprovalRequest.DEFAULT_EXPIRY;

        if (expiresIn < MIN_EXPIRY || expiresIn > ApprovalRequest.MAX_EXPIRY)
            throw new ArgumentException(
                "An approval waits at least a minute and at most 30 days.",
                nameof(request)
            );

        var arguments = WriteArguments(request.Arguments);
        var now = Now;
        var row = new ApprovalEntity
        {
            Kind = request.Kind,
            PluginKey = plugin,
            Summary = request.Summary.Trim(),
            Arguments = arguments,
            RequesterKind = requester.Kind.ToString(),
            RequesterPlayerId = requester.OnBehalfOf?.Value,
            RequesterName = requester.Name,
            Node = request.Node,
            State = nameof(ApprovalState.Pending),
            CreatedAt = now,
            ExpiresAt = now + expiresIn,
        };

        {
            var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            db.Approvals.Add(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var approval = ToApproval(row);

        logger.LogInformation(
            "Approval {Id} ({Kind}) asked by {Requester} for {Plugin}: {Summary}",
            approval.Id,
            approval.Kind,
            requester,
            plugin ?? ApprovalHandlers.CORE,
            approval.Summary
        );
        Publish(new ApprovalRequestedEvent { Approval = approval });

        return approval;
    }

    public async Task<Approval?> GetAsync(int id, CancellationToken ct)
    {
        var row = await LoadAsync(id, ct).ConfigureAwait(false);

        if (row is null)
            return null;

        if (IsOverdue(row))
            await ExpireAsync(row.Id, ct).ConfigureAwait(false);
        else
            return ToApproval(row);

        return await LoadAsync(id, ct).ConfigureAwait(false) is { } now ? ToApproval(now) : null;
    }

    public async Task<ApprovalList> ListAsync(ApprovalQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        await ExpireDueAsync(ct).ConfigureAwait(false);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rows = db.Approvals.AsNoTracking();

        if (query.State?.ToString() is { } state)
            rows = rows.Where(x => x.State == state);

        if (!string.IsNullOrEmpty(query.Kind))
            rows = rows.Where(x => x.Kind == query.Kind);

        if (query.Plugin is ApprovalHandlers.CORE)
            rows = rows.Where(x => x.PluginKey == null);
        else if (!string.IsNullOrEmpty(query.Plugin))
            rows = rows.Where(x => x.PluginKey == query.Plugin);

        var total = await rows.CountAsync(ct).ConfigureAwait(false);
        var entries = await rows.OrderByDescending(x => x.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new ApprovalList(total, [.. entries.Select(ToApproval)]);
    }

    public async Task<int> CountPendingAsync(CancellationToken ct)
    {
        var now = Now;

        var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .Approvals.Where(x => x.State == nameof(ApprovalState.Pending) && x.ExpiresAt > now)
            .CountAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<ApprovalDecision> ApproveAsync(
        int id,
        PlayerId by,
        string? note,
        CancellationToken ct
    )
    {
        var (refusal, row) = await CheckDeciderAsync(id, by, ct).ConfigureAwait(false);

        if (refusal is not null)
            return refusal;

        if (handlers.Find(row!.Kind) is not { } found)
            return new ApprovalDecision(
                ApprovalDecisionStatus.NoHandler,
                $"Nothing can carry out {row.Kind} now{(row.PluginKey is { } key ? $": is the plugin {key} loaded" : string.Empty)}? It stays pending.",
                ToApproval(row)
            );

        var decided = await ClaimAsync(row.Id, ApprovalState.Approved, by, note, ct)
            .ConfigureAwait(false);

        if (decided is null)
            return await NotPendingAsync(id, ct).ConfigureAwait(false);

        Publish(new ApprovalDecidedEvent { Approval = decided });

        // Claimed: it runs now, once, whatever becomes of the request that approved it.
        var result = await ExecuteAsync(found.Handler, found.Owner, decided).ConfigureAwait(false);
        var state = result.Succeeded ? ApprovalState.Executed : ApprovalState.Failed;
        var message = Cut(result.Message, ApprovalResult.MESSAGE_MAX_LENGTH);
        var executedAt = Now;
        var stateName = state.ToString();

        {
            var db = await dbFactory
                .CreateDbContextAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            await db
                .Approvals.Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.State, stateName)
                            .SetProperty(x => x.ExecutedAt, executedAt)
                            .SetProperty(x => x.Result, message),
                    CancellationToken.None
                )
                .ConfigureAwait(false);
        }

        var executed = decided with { State = state, ExecutedAtUtc = executedAt, Result = message };

        Publish(new ApprovalExecutedEvent { Approval = executed });

        return result.Succeeded
            ? new ApprovalDecision(
                ApprovalDecisionStatus.Executed,
                message is { Length: > 0 } ? $"Approved and done: {message}" : "Approved and done.",
                executed
            )
            : new ApprovalDecision(
                ApprovalDecisionStatus.Failed,
                $"Approved, but it failed: {message}",
                executed
            );
    }

    public async Task<ApprovalDecision> DenyAsync(
        int id,
        PlayerId by,
        string? note,
        CancellationToken ct
    )
    {
        var (refusal, row) = await CheckDeciderAsync(id, by, ct).ConfigureAwait(false);

        if (refusal is not null)
            return refusal;

        var decided = await ClaimAsync(row!.Id, ApprovalState.Denied, by, note, ct)
            .ConfigureAwait(false);

        if (decided is null)
            return await NotPendingAsync(id, ct).ConfigureAwait(false);

        Publish(new ApprovalDecidedEvent { Approval = decided });

        return new ApprovalDecision(ApprovalDecisionStatus.Denied, "Denied.", decided);
    }

    /// <summary>Expires every pending request past its time; answers how many.</summary>
    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = Now;
        int[] due;

        {
            var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            due = await db
                .Approvals.Where(x =>
                    x.State == nameof(ApprovalState.Pending) && x.ExpiresAt <= now
                )
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .Take(500)
                .ToArrayAsync(ct)
                .ConfigureAwait(false);
        }

        var expired = 0;

        foreach (var id in due)
            if (await ExpireAsync(id, ct).ConfigureAwait(false))
                expired++;

        return expired;
    }

    private async Task<(ApprovalDecision? Refusal, ApprovalEntity? Row)> CheckDeciderAsync(
        int id,
        PlayerId by,
        CancellationToken ct
    )
    {
        var row = await LoadAsync(id, ct).ConfigureAwait(false);

        if (row is null)
            return (
                new ApprovalDecision(
                    ApprovalDecisionStatus.NotFound,
                    $"There is no approval {id}."
                ),
                null
            );

        if (IsOverdue(row))
        {
            await ExpireAsync(row.Id, ct).ConfigureAwait(false);

            return (await NotPendingAsync(id, ct).ConfigureAwait(false), null);
        }

        if (row.State != nameof(ApprovalState.Pending))
            return (await NotPendingAsync(id, ct).ConfigureAwait(false), null);

        if (row.RequesterPlayerId == by.Value)
            return (
                new ApprovalDecision(
                    ApprovalDecisionStatus.OwnRequest,
                    "You asked for this, or it was asked for you: someone else decides it.",
                    ToApproval(row)
                ),
                null
            );

        var hotel = host.GetRequiredService<IHotel>();

        if (!await hotel.Players.HasPermissionAsync(by, row.Node, ct).ConfigureAwait(false))
            return (
                new ApprovalDecision(
                    ApprovalDecisionStatus.NotPermitted,
                    $"Deciding this needs {row.Node}, which you don't hold.",
                    ToApproval(row)
                ),
                null
            );

        return (null, row);
    }

    /// <summary>
    /// Moves the request from pending to <paramref name="state"/> in one update that only a
    /// pending, unexpired row matches; null when another decision (or the clock) got there first.
    /// </summary>
    private async Task<Approval?> ClaimAsync(
        int id,
        ApprovalState state,
        PlayerId by,
        string? note,
        CancellationToken ct
    )
    {
        var now = Now;
        var stateName = state.ToString();
        var text = Cut(note?.Trim(), 500) is { Length: > 0 } cut ? cut : null;

        var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var claimed = await db
            .Approvals.Where(x =>
                x.Id == id && x.State == nameof(ApprovalState.Pending) && x.ExpiresAt > now
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.State, stateName)
                        .SetProperty(x => x.DecidedByPlayerId, by.Value)
                        .SetProperty(x => x.DecidedAt, now)
                        .SetProperty(x => x.Note, text),
                ct
            )
            .ConfigureAwait(false);

        if (claimed == 0)
            return null;

        logger.LogInformation("Approval {Id} {State} by player {Player}", id, state, by.Value);

        return await LoadAsync(id, ct).ConfigureAwait(false) is { } row ? ToApproval(row) : null;
    }

    private async Task<bool> ExpireAsync(int id, CancellationToken ct)
    {
        var now = Now;
        int expired;

        {
            var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);
            expired = await db
                .Approvals.Where(x =>
                    x.Id == id && x.State == nameof(ApprovalState.Pending) && x.ExpiresAt <= now
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.State, nameof(ApprovalState.Expired))
                            .SetProperty(x => x.DecidedAt, now),
                    ct
                )
                .ConfigureAwait(false);
        }

        if (expired == 0)
            return false;

        if (await LoadAsync(id, ct).ConfigureAwait(false) is { } row)
            Publish(new ApprovalDecidedEvent { Approval = ToApproval(row) });

        return true;
    }

    private async Task<ApprovalResult> ExecuteAsync(
        IApprovalHandler handler,
        string owner,
        Approval approval
    )
    {
        try
        {
            var arguments =
                approval.Arguments.Deserialize(handler.ArgumentsType, ApprovalJson.Options)
                ?? throw new JsonException("The arguments are empty.");

            return await handler
                    .ExecuteAsync(arguments, approval, CancellationToken.None)
                    .ConfigureAwait(false)
                ?? ApprovalResult.Done();
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Approval {Id}'s arguments don't fit {Owner}'s handler of {Kind}",
                approval.Id,
                owner,
                approval.Kind
            );

            return ApprovalResult.Failed(
                $"Its arguments don't fit what {approval.Kind} takes: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Approval {Id}'s handler of {Kind} ({Owner}) failed",
                approval.Id,
                approval.Kind,
                owner
            );

            return ApprovalResult.Failed(
                $"Carrying out {approval.Kind} failed on the server; its error is in the server's log."
            );
        }
    }

    private async Task<ApprovalDecision> NotPendingAsync(int id, CancellationToken ct)
    {
        var row = await LoadAsync(id, ct).ConfigureAwait(false);
        var approval = row is null ? null : ToApproval(row);

        return new ApprovalDecision(
            ApprovalDecisionStatus.NotPending,
            approval?.State switch
            {
                ApprovalState.Expired => "It expired before anyone decided.",
                ApprovalState.Denied => "Someone denied it already.",
                null => $"There is no approval {id}.",
                _ => "Someone approved it already.",
            },
            approval
        );
    }

    private async Task<ApprovalEntity?> LoadAsync(int id, CancellationToken ct)
    {
        var db = await dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .Approvals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);
    }

    private bool IsOverdue(ApprovalEntity row) =>
        row.State == nameof(ApprovalState.Pending) && row.ExpiresAt <= Now;

    private void Publish(DomainEvent evt) =>
        host.GetService<IEventPublisher>()?.PublishCommitted(evt);

    private static string WriteArguments(object? arguments)
    {
        var element = arguments switch
        {
            null => JsonSerializer.SerializeToElement(new { }),
            JsonElement json => json,
            JsonDocument doc => doc.RootElement,
            _ => JsonSerializer.SerializeToElement(
                arguments,
                arguments.GetType(),
                ApprovalJson.Options
            ),
        };

        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                $"An approval's arguments are an object (a record or class), not {element.ValueKind}.",
                nameof(arguments)
            );

        var text = element.GetRawText();

        if (text.Length > ApprovalRequest.ARGUMENTS_MAX_LENGTH)
            throw new ArgumentException(
                $"An approval's arguments take at most {ApprovalRequest.ARGUMENTS_MAX_LENGTH} characters as JSON; these take {text.Length}.",
                nameof(arguments)
            );

        return text;
    }

    private static string? Cut(string? text, int max) =>
        text is { Length: > 0 } && text.Length > max ? text[..max] : text;

    private static Approval ToApproval(ApprovalEntity row)
    {
        using var args = JsonDocument.Parse(row.Arguments);

        return new Approval
        {
            Id = row.Id,
            Kind = row.Kind,
            Plugin = row.PluginKey,
            Summary = row.Summary,
            Arguments = args.RootElement.Clone(),
            Requester = new ApprovalRequester(
                Enum.TryParse<ApprovalRequesterKind>(row.RequesterKind, out var kind)
                    ? kind
                    : ApprovalRequesterKind.System,
                row.RequesterPlayerId is { } player ? PlayerId.Parse(player) : null,
                row.RequesterName
            ),
            Node = row.Node,
            State = Enum.TryParse<ApprovalState>(row.State, out var state)
                ? state
                : ApprovalState.Failed,
            RequestedAtUtc = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
            ExpiresAtUtc = DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc),
            DecidedBy = row.DecidedByPlayerId is { } by ? PlayerId.Parse(by) : null,
            DecidedAtUtc = Utc(row.DecidedAt),
            Note = row.Note,
            ExecutedAtUtc = Utc(row.ExecutedAt),
            Result = row.Result,
        };
    }

    private static DateTime? Utc(DateTime? at) =>
        at is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : null;
}

/// <summary>A plugin's <see cref="IApprovals"/>: what it asks is recorded as its own.</summary>
internal sealed class PluginApprovals(HotelApprovals approvals, string key) : IApprovals
{
    public Task<Approval> RequestAsync(ApprovalRequest request, CancellationToken ct) =>
        approvals.RequestAsync(request, key, ct);

    public Task<Approval?> GetAsync(int id, CancellationToken ct) => approvals.GetAsync(id, ct);

    public Task<ApprovalList> ListAsync(ApprovalQuery query, CancellationToken ct) =>
        approvals.ListAsync(query, ct);

    public Task<int> CountPendingAsync(CancellationToken ct) => approvals.CountPendingAsync(ct);

    public Task<ApprovalDecision> ApproveAsync(
        int id,
        PlayerId by,
        string? note,
        CancellationToken ct
    ) => approvals.ApproveAsync(id, by, note, ct);

    public Task<ApprovalDecision> DenyAsync(
        int id,
        PlayerId by,
        string? note,
        CancellationToken ct
    ) => approvals.DenyAsync(id, by, note, ct);
}
