using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Landingview.Votes;

public record CommunityGoalVoteMessage : IMessageEvent
{
    /// <summary>The side voted for: 1 or 2, as <c>CommunityGoalVsModeWidgetWithVoting</c> sends.</summary>
    public required int VoteOption { get; init; }
}
