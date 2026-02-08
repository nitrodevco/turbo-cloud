using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Avatar;

[GenerateSerializer, Immutable]
public sealed record FigureUpdateEventMessageComposer : IComposer
{
    [Id(0)]
    public required string Figure { get; init; }

    [Id(1)]
    public required AvatarGenderType Gender { get; init; }
}
