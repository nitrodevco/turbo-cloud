using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>
/// What a viewer wants of the room's leaderboards (Flash
/// <c>VariableLeaderboardReplaceSubscriptionMessageComposer</c>): an overlay, its query revision,
/// the variable picked on it and the chunks of 50 rows shown (at most two, distinct, never
/// negative). It replaces whatever the viewer had. On room entry the client sends (0, 0, "0", [])
/// and is answered with the room's leaderboards (<c>VariableLeaderboardController.as</c> 229).
/// </summary>
public record VariableLeaderboardReplaceSubscriptionMessage : IMessageEvent
{
    public required int OverlayInstanceId { get; init; }
    public required int QueryRevision { get; init; }
    public required string SelectedVariableId { get; init; }
    public required ImmutableArray<int> ChunkIndices { get; init; }
}
