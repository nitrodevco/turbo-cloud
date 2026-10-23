using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Game.Directory;

public record Game2StartSnowWarMessage : IMessageEvent
{
    /// <summary>The game to join, by its key.</summary>
    public required string Key { get; init; }
}
