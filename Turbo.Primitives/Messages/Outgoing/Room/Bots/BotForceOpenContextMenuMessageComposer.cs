using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Bots;

[GenerateSerializer, Immutable]
public sealed record BotForceOpenContextMenuMessageComposer : IComposer
{
    [Id(0)]
    public required int BotId { get; init; }
}
