using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Quests.Grains;

/// <summary>
/// A player's quests: each campaign's current quest, the one quest they are doing, and its
/// progress. Every answer is sent to the player.
/// </summary>
public interface IPlayerQuestGrain : IGrainWithIntegerKey
{
    /// <summary>Sends each campaign's current quest (Quests), opening the window or not.</summary>
    Task SendQuestsAsync(bool openWindow, CancellationToken ct);

    /// <summary>Makes the quest the one the player is doing, dropping any other, and sends it.</summary>
    Task AcceptAsync(int questId, CancellationToken ct);

    /// <summary>Drops the quest (or, with 0, whichever the player is doing): QuestCancelled.</summary>
    Task RejectAsync(int questId, CancellationToken ct);

    /// <summary>
    /// The tracker asks for the quest to show after one is completed (OpenQuestTracker): the one
    /// being done, else the next of the campaign last completed in, which is accepted. Nothing
    /// when that campaign is done; the tracker then hides.
    /// </summary>
    Task OpenTrackerAsync(CancellationToken ct);

    /// <summary>
    /// Counts an action towards the quest the player is doing, when it is of that type (and, for
    /// a quest with a target, that value). Sends the progress, or QuestCompleted and the reward.
    /// </summary>
    Task RecordAsync(string type, string value, CancellationToken ct);
}
