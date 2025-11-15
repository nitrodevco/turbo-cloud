using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms.Furniture;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record CustomUserNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required CustomUserNotificationType Code { get; init; }
}
