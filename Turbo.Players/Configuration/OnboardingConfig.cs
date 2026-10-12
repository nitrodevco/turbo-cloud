using System.Collections.Generic;

namespace Turbo.Players.Configuration;

/// <summary>
/// The new user flow Habbo shows a player's first login: "Choose your style" (look and name) and
/// "Choose your room" (a furnished starter room). Login tells the client which steps are still to
/// do through <c>AuthenticationOK</c>'s suggested login actions (0 = name and look, 1 = room),
/// and the client draws the flow only when one is there. Off by default; a player created while
/// it is on keeps their steps until they finish them.
///
/// What the client offers is the hotel's client configuration: the room types
/// (<c>new.user.flow.roomTypes</c>), their pictures and texts, and the looks the dice rolls. This
/// section is what the server does with the answers. Names are checked by
/// <c>Turbo:Players:BlockedNames</c> as well as the sign-up's rules.
/// </summary>
public sealed class OnboardingConfig
{
    public const string SECTION_NAME = "Turbo:Onboarding";

    /// <summary>Sends new players through the flow. Off sends no steps, so no one sees it.</summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Offers the look and name step. Turbo's sign-up already takes a name, so the player may keep
    /// it (checking their own name answers "available") or pick another one, once.
    /// </summary>
    public bool ChooseName { get; init; } = true;

    /// <summary>Offers the starter room step.</summary>
    public bool PickRoom { get; init; } = true;

    /// <summary>
    /// The starter rooms, by the room type the client sends (its <c>new.user.flow.roomTypes</c>).
    /// An entry with a <c>RoomId</c> copies that room of the hotel - build it like any room, then
    /// name it here; one without uses Turbo's built-in template of that type. Empty offers the
    /// built-in templates (Habbo's room 10).
    /// </summary>
    public List<StarterRoomOption> StarterRooms { get; init; } = [];

    /// <summary>Names suggested for a taken one; Habbo sends three. 0 suggests none.</summary>
    public int NameSuggestions { get; init; } = 3;

    /// <summary>
    /// Words a taken name is combined with for its suggestions ("HutchCyclone", "PurpleBill").
    /// Empty uses Turbo's own few.
    /// </summary>
    public List<string> SuggestionWords { get; init; } = [];
}

/// <summary>One starter room a new player may pick.</summary>
public sealed class StarterRoomOption
{
    /// <summary>The room type the client sends for it ("10").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// The hotel room copied for each player who picks it: its model (a floor plan of its own
    /// included), paint, walls and furni. 0 uses the built-in template of <see cref="Type"/>.
    /// </summary>
    public int RoomId { get; init; }

    /// <summary>The new room's name; <c>%name%</c> is the player's. Empty keeps the source's.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The new room's description; <c>%name%</c> is the player's. Empty keeps the source's.</summary>
    public string Description { get; init; } = string.Empty;
}
