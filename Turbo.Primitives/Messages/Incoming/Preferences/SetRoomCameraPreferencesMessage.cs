using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetRoomCameraPreferencesMessage : IMessageEvent
{
    public bool CameraFollowDisabled { get; init; }
}
