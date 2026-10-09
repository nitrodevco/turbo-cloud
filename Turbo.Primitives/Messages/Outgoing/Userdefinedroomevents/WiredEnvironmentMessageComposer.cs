using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>
/// Whether the room has a "user clicks user" trigger. While it does, the client reports clicks on
/// avatars with <c>WiredClickUser</c>, holds the avatar menu until the room answers, and stops
/// turning its own avatar towards whoever it clicked (Flash <c>WiredEnvironment</c>,
/// <c>RoomObjectEventHandler.setSelectedAvatar</c>). The client reads an achievement list after
/// the flag only when there are bytes left; this hotel has none to send.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredEnvironmentMessageComposer : IComposer
{
    [Id(0)]
    public required bool HasClickUserWired { get; init; }
}
