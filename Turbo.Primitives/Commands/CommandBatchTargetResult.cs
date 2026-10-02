using Turbo.Primitives.Players;

namespace Turbo.Primitives.Commands;

/// <summary>Confirmed results for one player's requested operations. An indeterminate call may
/// have committed, so it must never be retried automatically.</summary>
public sealed record CommandBatchTargetResult(
    PlayerId PlayerId,
    int Requested,
    int Succeeded,
    int Failed,
    int Indeterminate
)
{
    public int Unattempted => Requested - Succeeded - Failed - Indeterminate;

    public bool Completed => Succeeded == Requested;

    public bool Partial => Succeeded > 0 && !Completed;
}
