using System.Collections.Generic;
using Turbo.Primitives.Quests.RewardTracks;

namespace Turbo.Achievements.Configuration;

/// <summary>
/// Reward track settings. The tracks themselves are in the database (<see cref="IRewardTrackStore"/>,
/// the panel's Reward Tracks); Habbo's Introduction and Habboween 2026 tracks are put there when the
/// tables are made.
/// </summary>
public sealed class RewardTrackConfig
{
    public const string SECTION_NAME = "Turbo:RewardTracks";

    /// <summary>When off, the client is told reward tracks are disabled and claims are refused.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Tracks from before they were kept in the database: one the database lacks is moved into it
    /// when the server starts, and is changed in the panel from then on.
    /// </summary>
    public List<RewardTrackDefinition> Tracks { get; set; } = [];

    /// <summary>What a pet prize is called when its definition names none.</summary>
    public string PetPrizeName { get; set; } = "Pet";

    /// <summary>What a bot prize is called when its definition names none.</summary>
    public string BotPrizeName { get; set; } = "Bot";

    /// <summary>What is wrong with the configured tracks, or null when they can be used.</summary>
    public string? Problem() => RewardTrackRules.Problem(Tracks);
}
