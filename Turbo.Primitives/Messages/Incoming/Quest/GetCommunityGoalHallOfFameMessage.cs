using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

public record GetCommunityGoalHallOfFameMessage : IMessageEvent
{
    /// <summary>The goal whose best contributors are asked for.</summary>
    public required string GoalCode { get; init; }
}
