using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record ExtendedProfileMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerExtendedProfileSnapshot Profile { get; init; }
}
