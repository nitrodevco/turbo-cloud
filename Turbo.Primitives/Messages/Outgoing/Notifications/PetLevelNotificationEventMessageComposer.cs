using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record PetLevelNotificationEventMessageComposer : IComposer
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int Level { get; init; }

    [Id(3)]
    public required PetFigureSnapshot Figure { get; init; }
}
