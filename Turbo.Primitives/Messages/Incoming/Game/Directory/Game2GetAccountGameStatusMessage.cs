using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Game.Directory;

public record Game2GetAccountGameStatusMessage : IMessageEvent
{
    public required int GameTypeId { get; init; }
}
