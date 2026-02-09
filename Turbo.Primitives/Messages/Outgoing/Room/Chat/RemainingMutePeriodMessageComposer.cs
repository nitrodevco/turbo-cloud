using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public sealed record RemainingMutePeriodMessageComposer : IComposer
{
    [Id(0)]
    public required int SecondsRemaining { get; init; }
}
