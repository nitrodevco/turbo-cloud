namespace Turbo.Primitives.Networking;

/// <summary>
/// A message the client sends on a timer of its own, whether or not the player does anything: a
/// Pong, the latency test, the friend list and forum polls. It shows the client is there, but not
/// that the player is, so it does not keep a session from being disconnected as idle
/// (<see cref="ISessionContext.LastActiveUtc"/>).
/// </summary>
public interface IClientTimerMessage : IMessageEvent;
