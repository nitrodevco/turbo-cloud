using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// What one player in the room has been sent: the statuses their client is showing, by
/// signature, and the messages waiting for the next batch. Bounded by
/// <c>WiredConfig.VariableFxMaxStatusesPerViewer</c>, and dropped when the player leaves.
/// </summary>
internal sealed class VariableFxViewer
{
    public Dictionary<VariableFxStatusKeySnapshot, string> Sent { get; } = [];

    public List<IComposer> Outbox { get; } = [];

    /// <summary>False until the first status batch, which the client takes as a sync rather than as changes.</summary>
    public bool IsSynced { get; set; }
}
