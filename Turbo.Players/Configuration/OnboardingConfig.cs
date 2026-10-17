using System.Collections.Generic;

namespace Turbo.Players.Configuration;

/// <summary>
/// The new user flow Habbo shows a player's first login: "Choose your style" (look and name) and
/// "Choose your room" (a furnished starter room). Login tells the client which steps are still to
/// do through <c>AuthenticationOK</c>'s suggested login actions (0 = name and look, 1 = room),
/// and the client draws the flow only when one is there. Off by default; a player created while
/// it is on keeps their steps until they finish them.
///
/// The starter rooms are rooms built in the hotel and offered with <c>:starterroom</c>, which keeps
/// the client's room types, pictures and texts in step; with none, the built-in ones are offered.
/// The looks the dice rolls are the client's configuration. Names are checked by
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
    /// The most starter rooms <c>:starterroom add</c> offers; Habbo's room picker is laid out
    /// for three side by side.
    /// </summary>
    public int MaxStarterRooms { get; init; } = 3;

    /// <summary>
    /// The folder starter room pictures are drawn into (one PNG per room, served by the gamedata
    /// host at <c>/gamedata/starter-rooms/&lt;room id&gt;.png</c>), with each picture's render data.
    /// </summary>
    public string PictureDirectory { get; init; } = "camera/starter-rooms";

    /// <summary>Names suggested for a taken one; Habbo sends three. 0 suggests none.</summary>
    public int NameSuggestions { get; init; } = 3;

    /// <summary>
    /// Words a taken name is combined with for its suggestions ("HutchCyclone", "PurpleBill").
    /// Empty uses Turbo's own few.
    /// </summary>
    public List<string> SuggestionWords { get; init; } = [];

    /// <summary>
    /// How long, from sign-up, a player is a new user to the client (<c>NoobnessLevel</c> 2, "real
    /// noob"): the noob lobby offer, the toolbar's way back to the lobby, the rooms only new users
    /// may enter. Habbo's length is not known; 0 makes no one new. Not tied to <see cref="Enabled"/>.
    /// </summary>
    public int NewUserHours { get; init; } = 24;
}
