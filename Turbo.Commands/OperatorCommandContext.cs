using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;

namespace Turbo.Commands;

/// <summary>
/// What an operator command is given. It resolves the names a line holds across the whole hotel,
/// because the player a staff member names may be offline or in another room, and it remembers
/// whether the line used a selector so the runner logs it.
/// </summary>
internal sealed class OperatorCommandContext(
    IOperatorExecutor executor,
    string argumentText,
    string? selectorNode,
    bool isConfirmed,
    int confirmAtPlayers,
    Func<CancellationToken, Task<CommandResult>> confirmPending,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ICommandBatchExecutor batchExecutor,
    IPlayerNoticeService notices,
    string commandName,
    IReadOnlyDictionary<string, IReadOnlyList<PlayerId>>? audiences = null
) : IOperatorCommandContext
{
    private readonly Dictionary<string, IReadOnlyList<PlayerId>> _audiences = audiences is null
        ? new(StringComparer.OrdinalIgnoreCase)
        : new(audiences, StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyDictionary<string, IReadOnlyList<PlayerId>> Audiences => _audiences;

    internal Guid ExecutionId { get; } = Guid.NewGuid();

    internal Guid? ParentExecutionId { get; init; }

    internal Guid? ConfirmationId { get; set; }

    internal string? RequiredPermission { get; set; }

    private int _noticeFailures;
    private int _offlineNotices;
    internal int NoticeFailures => _noticeFailures;
    internal int OfflineNotices => _offlineNotices;

    public async Task NotifyAsync(
        PlayerId playerId,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    )
    {
        var delivery = await notices.SendAsync(playerId, textKey, defaultText, parameters, ct);
        if (delivery == PlayerNoticeDelivery.Failed)
            Interlocked.Increment(ref _noticeFailures);
        else if (delivery == PlayerNoticeDelivery.Offline)
            Interlocked.Increment(ref _offlineNotices);
    }

    public IOperatorExecutor Executor { get; } = executor;

    public string ArgumentText { get; } = argumentText;

    public bool IsConfirmed { get; } = isConfirmed;

    public bool ShouldConfirm(int players) => !IsConfirmed && players >= confirmAtPlayers;

    public Task<CommandBatchResult> ExecuteBatchAsync(
        IReadOnlyList<ResolvedPlayer> players,
        int unitsPerPlayer,
        Func<ResolvedPlayer, CancellationToken, ValueTask<bool>> operation,
        CancellationToken ct
    ) => batchExecutor.ExecuteAsync(commandName, players, unitsPerPlayer, operation, ct);

    public IReadOnlyList<PlayerId> SnapshotRecipients(
        string scope,
        IEnumerable<PlayerId> recipients
    )
    {
        if (!_audiences.TryGetValue(scope, out var ids))
        {
            if (IsConfirmed)
                throw new InvalidOperationException(
                    "A confirmed command cannot add an audience that was not captured before confirmation."
                );

            ids = Array.AsReadOnly(recipients.Distinct().ToArray());
            _audiences.Add(scope, ids);
        }

        return ids;
    }

    /// <summary>What <see cref="ConfirmCommand"/> calls: the runner's own, for this executor.</summary>
    internal Task<CommandResult> ConfirmPendingAsync(CancellationToken ct) => confirmPending(ct);

    /// <summary>The line named a group of players, so it is logged whoever ran it.</summary>
    public bool UsedSelector { get; private set; } =
        audiences?.Keys.Any(scope =>
            scope.StartsWith("selector:", StringComparison.OrdinalIgnoreCase)
        ) == true;

    public async Task<TargetSelection> ResolveAsync(PlayerTarget target, CancellationToken ct)
    {
        if (target.IsSelector)
            return TargetSelection.Refused(
                CommandResult.Fail(CommandReplyKeys.SELECTOR_REFUSED, target.Text)
            );

        var directory = grainFactory.GetPlayerDirectoryGrain();

        var scope = "player:" + target.Text;

        if (!_audiences.TryGetValue(scope, out var ids))
        {
            if (await directory.GetPlayerIdAsync(target.Text, ct) is not { } id)
                return TargetSelection.Refused(
                    CommandResult.Fail(CommandReplyKeys.PLAYER_NOT_FOUND, target.Text)
                );

            ids = SnapshotRecipients(scope, [id]);
        }

        var playerId = ids[0];

        // The name as the hotel spells it, not as it was typed.
        return TargetSelection.Of(
            new ResolvedPlayer(playerId, await directory.GetPlayerNameAsync(playerId, ct))
        );
    }

    public async Task<TargetSelection> SelectAsync(PlayerTarget target, CancellationToken ct)
    {
        if (!target.IsSelector)
            return await ResolveAsync(target, ct);

        // A command that declares no selector takes none, whoever runs it.
        if (selectorNode is null || !await Executor.HasAsync(selectorNode, ct))
            return TargetSelection.Refused(
                CommandResult.Fail(CommandReplyKeys.SELECTOR_REFUSED, target.Text)
            );

        IReadOnlyCollection<PlayerId> ids;

        if (target.Text.Equals(PlayerTarget.ROOM, StringComparison.OrdinalIgnoreCase))
        {
            if (Executor.RoomId is null)
                return TargetSelection.Refused(CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM));

            ids = Executor.RoomPlayerIds;
        }
        else if (target.Text.Equals(PlayerTarget.ONLINE, StringComparison.OrdinalIgnoreCase))
        {
            ids = sessionGateway.GetOnlinePlayerIds();
        }
        else
        {
            return TargetSelection.Refused(
                CommandResult.Fail(CommandReplyKeys.SELECTOR_UNKNOWN, target.Text)
            );
        }

        var selectedIds = SnapshotRecipients("selector:" + target.Text, ids);

        UsedSelector = true;

        if (ShouldConfirm(selectedIds.Count))
            return TargetSelection.Refused(
                CommandResult.Confirm(
                    CommandReplyKeys.CONFIRM_SELECTOR,
                    selectedIds.Count.ToString()
                )
            );

        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync([.. selectedIds], ct);

        return TargetSelection.Many([
            .. selectedIds.Select(id => new ResolvedPlayer(
                id,
                names.TryGetValue(id, out var name) ? name : id.ToString()
            )),
        ]);
    }
}
