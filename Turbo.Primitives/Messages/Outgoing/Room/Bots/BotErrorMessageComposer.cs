using Orleans;
using Turbo.Primitives.Bots.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Bots;

[GenerateSerializer, Immutable]
public sealed record BotErrorMessageComposer : IComposer
{
    [Id(0)]
    public required BotErrorType Error { get; init; }
}
