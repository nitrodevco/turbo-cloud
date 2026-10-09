using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Editing;

public sealed partial class CatalogEditService
{
    /// <summary>
    /// The most steps kept to undo; older ones are let go, and discarding then stops short of
    /// the published catalog.
    /// </summary>
    public const int HISTORY_MAX = 500;

    private readonly object _historyLock = new();
    private readonly List<CatalogEditStep> _undo = [];
    private readonly List<CatalogEditStep> _redo = [];
    private readonly SemaphoreSlim _historyGate = new(1, 1);
    private readonly AsyncLocal<CatalogEditStep?> _group = new();
    private bool _truncated;

    public CatalogHistory History
    {
        get
        {
            lock (_historyLock)
            {
                return new CatalogHistory(
                    [.. Enumerable.Reverse(_undo).Select(x => x.Entry)],
                    [.. Enumerable.Reverse(_redo).Select(x => x.Entry)],
                    _truncated
                );
            }
        }
    }

    public async Task<T> GroupAsync<T>(PlayerId editor, string label, Func<Task<T>> edits)
    {
        // Inside a group already, the edits are that group's.
        if (_group.Value is not null)
            return await edits().ConfigureAwait(false);

        var step = new CatalogEditStep(label, editor, Now);

        _group.Value = step;

        try
        {
            return await edits().ConfigureAwait(false);
        }
        finally
        {
            _group.Value = null;

            if (step.Changed)
                Push(step);
        }
    }

    public Task<CatalogEditResult> UndoAsync(PlayerId editor, CancellationToken ct) =>
        StepAsync(editor, undo: true, ct);

    public Task<CatalogEditResult> RedoAsync(PlayerId editor, CancellationToken ct) =>
        StepAsync(editor, undo: false, ct);

    public async Task<CatalogEditResult> DiscardAsync(PlayerId editor, CancellationToken ct)
    {
        await _historyGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var undone = 0;

            while (true)
            {
                CatalogEditStep? step;

                lock (_historyLock)
                    step = _undo.LastOrDefault();

                if (step is null)
                    break;

                if (await MoveStepAsync(step, undo: true, ct).ConfigureAwait(false) is { } error)
                {
                    return CatalogEditResult.Refused(
                        undone == 0
                            ? error
                            : $"{undone} {(undone == 1 ? "change was" : "changes were")} thrown away, then: {error}"
                    );
                }

                undone++;
            }

            logger.LogInformation(
                "Player {PlayerId} threw away {Steps} unpublished catalog changes",
                editor,
                undone
            );

            return CatalogEditResult.Done(undone);
        }
        finally
        {
            _historyGate.Release();
        }
    }

    private async Task<CatalogEditResult> StepAsync(
        PlayerId editor,
        bool undo,
        CancellationToken ct
    )
    {
        await _historyGate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            CatalogEditStep? step;

            lock (_historyLock)
                step = (undo ? _undo : _redo).LastOrDefault();

            if (step is null)
                return CatalogEditResult.Refused(
                    undo ? "There is nothing to undo." : "There is nothing to redo."
                );

            if (await MoveStepAsync(step, undo, ct).ConfigureAwait(false) is { } error)
                return CatalogEditResult.Refused(error);

            logger.LogInformation(
                "Player {PlayerId} {Action} in the catalog: {Step}",
                editor,
                undo ? "undid" : "redid",
                step.Label
            );

            return CatalogEditResult.Done(0);
        }
        finally
        {
            _historyGate.Release();
        }
    }

    /// <summary>Puts a step's rows back, or forward again, and moves it to the other list.</summary>
    private async Task<string?> MoveStepAsync(CatalogEditStep step, bool undo, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using (db.ConfigureAwait(false))
        {
            if (
                await CatalogEditJournal.RestoreAsync(db, step, undo, ct).ConfigureAwait(false) is
                { } error
            )
                return error;
        }

        lock (_historyLock)
        {
            (undo ? _undo : _redo).Remove(step);
            (undo ? _redo : _undo).Add(step);
        }

        if (undo)
            Interlocked.Add(ref _unpublished, -Math.Min(step.Edits, UnpublishedChanges));
        else
            Interlocked.Add(ref _unpublished, step.Edits);

        return null;
    }

    /// <summary>A step done: it can be undone, and what was undone before it can't be redone.</summary>
    private void Push(CatalogEditStep step)
    {
        lock (_historyLock)
        {
            _undo.Add(step);
            _redo.Clear();

            if (_undo.Count > HISTORY_MAX)
            {
                _undo.RemoveAt(0);
                _truncated = true;
            }
        }
    }

    /// <summary>Published: what came before can no longer be undone.</summary>
    private void ForgetHistory()
    {
        lock (_historyLock)
        {
            _undo.Clear();
            _redo.Clear();
            _truncated = false;
        }
    }

    /// <summary>Saves the context's changes, read off as the journal keeps them.</summary>
    private static Task<IReadOnlyList<CatalogRowChange>> SaveAsync(
        TurboDbContext db,
        CancellationToken ct
    ) => CatalogEditJournal.SaveAsync(db, ct);

    private DateTime Now => (time ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    /// <summary>
    /// An edit saved: one more change to publish, a line in the log saying who, and a step to
    /// undo - or part of the group being made.
    /// </summary>
    private CatalogEditResult Changed(
        PlayerId editor,
        string label,
        int id,
        IReadOnlyList<CatalogRowChange> changes
    )
    {
        Interlocked.Increment(ref _unpublished);
        logger.LogInformation("Player {PlayerId} {Change} in the catalog", editor, label);

        if (_group.Value is { } group)
            group.Add(changes);
        else
        {
            var step = new CatalogEditStep(label, editor, Now);

            step.Add(changes);

            if (step.Changed)
                Push(step);
        }

        return CatalogEditResult.Done(id);
    }
}
