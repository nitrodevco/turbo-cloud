using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// The answer to <c>WiredClickUser</c>: the clicked avatar's room index and whether the client
/// may open its avatar menu (Flash <c>AvatarInfoWidget.onUserClickHandledEvent</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredClickUserResponseMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required bool OpenMenu { get; init; }
}
