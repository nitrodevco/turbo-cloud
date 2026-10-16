using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Quest;

/// <summary>
/// Polled every 5 s while the hotel view shows the users counter (ConcurrentUsersInfoElementHandler.as:51, 98).
/// </summary>
public record GetConcurrentUsersGoalProgressMessage : IClientTimerMessage { }
