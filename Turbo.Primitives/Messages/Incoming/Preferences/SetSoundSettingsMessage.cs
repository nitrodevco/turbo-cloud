using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetSoundSettingsMessage : IMessageEvent
{
    public int TraxVolume { get; init; }
    public int FurniVolume { get; init; }
    public int GenericVolume { get; init; }
}
