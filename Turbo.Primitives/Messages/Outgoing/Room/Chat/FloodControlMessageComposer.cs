using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record FloodControlMessageComposer : IComposer
{
    [Id(0)]
    public required int Seconds { get; init; }
}
