using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Preferences;

/// <summary>
/// The player's Discord preferences (WIN63-202610131318
/// <c>com/sulake/habbo/communication/messages/parser/discord/DiscordPreferences.as</c>
/// <c>readFromData</c>: version, then four booleans). Version 0 means never set; the client then
/// shows its own defaults, every box ticked (<c>DiscordSettingsController.as</c>
/// <c>PREFERENCES_DEFAULT</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DiscordPreferencesMessageComposer : IComposer
{
    [Id(0)]
    public required int Version { get; init; }

    [Id(1)]
    public required bool ShowHabbo { get; init; }

    [Id(2)]
    public required bool ShareActivity { get; init; }

    [Id(3)]
    public required bool HideInHiddenRooms { get; init; }

    [Id(4)]
    public required bool AllowJoining { get; init; }
}
