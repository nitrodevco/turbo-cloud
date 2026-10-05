using Orleans;

namespace Turbo.Primitives.Players.Enums;

/// <summary>How a temporary look is laid over a player's saved figure.</summary>
[GenerateSerializer]
public enum LookOverrideMode
{
    /// <summary>The override figure is shown whole; nothing of the saved figure remains.</summary>
    Replace = 0,

    /// <summary>
    /// The override's parts replace the saved parts of the same set type (<c>ch</c>, <c>lg</c>,
    /// ...) and every other saved part still shows, so a uniform can be worn over the player's
    /// own face and hair.
    /// </summary>
    MergeParts = 1,
}
