using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Handshake;

/// <summary>The answer to the server's Ping (IncomingMessages.as:254).</summary>
public record PongMessage : IClientTimerMessage { }
