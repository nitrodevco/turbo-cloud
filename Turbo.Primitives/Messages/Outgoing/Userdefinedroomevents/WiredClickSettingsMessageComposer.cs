using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Changes what one player's clicks on avatars and furni do in this room. The client keeps the
/// setting until it is told another one or leaves the room.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredClickSettingsMessageComposer : IComposer
{
    [Id(0)]
    public required WiredClickUserType UserOption { get; init; }

    [Id(1)]
    public required WiredClickFurniType FurniOption { get; init; }
}
