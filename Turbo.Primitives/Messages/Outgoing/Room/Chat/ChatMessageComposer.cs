using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public record ChatMessageComposer : IComposer
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required string Text { get; init; }

    [Id(2)]
    public required AvatarGestureType Gesture { get; init; }

    [Id(3)]
    public required int StyleId { get; init; }

    [Id(4)]
    public required List<(string, string, bool)> Links { get; init; }

    [Id(5)]
    public required int TrackingId { get; init; }
}
