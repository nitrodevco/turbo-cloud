using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

/// <summary>
/// Also polled by the hotel view's score counter (CommunityGoalScoreCounterElementHandler.as:71, 119).
/// </summary>
public record GetCommunityGoalProgressMessage : IClientTimerMessage { }
