using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Fades users (avatars, pets, bots) in or out for the players it is sent to (WIRED Effect: Set
/// Visibility on users). Flash <c>WiredUserTransparencyMessageParser</c>: the users' room indices,
/// then the same fields as <see cref="WiredFurniTransparencyMessageComposer"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredUserTransparencyMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<int> RoomIndices { get; init; }

    [Id(1)]
    public required int Transparency { get; init; }

    [Id(2)]
    public required bool AllowClickThrough { get; init; }

    [Id(3)]
    public required int EasingMode { get; init; }
}
