using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

/// <summary>
/// The Discord settings window's four boxes, sent when it closes, with the settings version the
/// client was configured with (<c>discord_activity.settings.version</c>).
/// </summary>
public record SetDiscordPreferencesMessage : IMessageEvent
{
    public int Version { get; init; }
    public bool ShowHabbo { get; init; }
    public bool ShareActivity { get; init; }
    public bool HideInHiddenRooms { get; init; }
    public bool AllowJoining { get; init; }
}
