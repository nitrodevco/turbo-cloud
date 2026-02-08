using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record ExtendedProfileChangedMessageComposer : IComposer
{
    [Id(0)]
    public required int UserId { get; init; }
}
