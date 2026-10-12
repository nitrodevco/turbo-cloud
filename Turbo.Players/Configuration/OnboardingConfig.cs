using System.Collections.Generic;

namespace Turbo.Players.Configuration;

/// <summary>
/// The new user flow Habbo shows a player's first login: "Choose your style" (look and name) and
/// "Choose your room" (a furnished starter room). Login tells the client which steps are still to
/// do through <c>AuthenticationOK</c>'s suggested login actions (0 = name and look, 1 = room),
/// and the client draws the flow only when one is there. Off by default; a player created while
/// it is on keeps their steps until they finish them.
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

    /// <summary>Names suggested for a taken one; Habbo sends three.</summary>
    public int NameSuggestions { get; init; } = 3;

    /// <summary>Words a taken name is combined with for its suggestions ("HutchCyclone", "PurpleBill").</summary>
    public List<string> SuggestionWords { get; init; } =
    [
        "Cool",
        "Star",
        "Pixel",
        "Sunny",
        "Purple",
        "Cyclone",
        "Bonsai",
        "Mushroom",
        "Mammoth",
        "Jellyfish",
        "Blizzard",
        "Boulder",
        "Comet",
        "Breeze",
        "Racer",
        "Smith",
    ];
}
