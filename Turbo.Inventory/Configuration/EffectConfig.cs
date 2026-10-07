using System.Collections.Generic;

namespace Turbo.Inventory.Configuration;

/// <summary>
/// Hotel settings for the avatar effects a player owns. The length of a use is the effect's,
/// not the grant's: the client shows one duration per effect type, so two grants of the same
/// type with different lengths could not both be right.
/// </summary>
public class EffectConfig
{
    public const string SECTION_NAME = "Turbo:Effects";

    /// <summary>Seconds one use of an effect lasts, unless <see cref="DurationOverrides"/> names it.</summary>
    public int DefaultDurationSeconds { get; init; } = 3600;

    /// <summary>Seconds one use lasts, by effect id, for the effects that differ from the default.</summary>
    public Dictionary<int, int> DurationOverrides { get; init; } = [];

    /// <summary>
    /// Effects that are costumes. The client reads sub type 1 to send a player who owns one to the
    /// avatar editor's effects tab instead of the costumes catalog page; the sub type is the
    /// hotel's to say, since nothing in an effect says what it is.
    /// </summary>
    public HashSet<int> CostumeEffectIds { get; init; } = [];

    /// <summary>The sub type an effect is stored with: 1 for a costume, else 0.</summary>
    public int GetSubType(int effectId, int requested) =>
        (CostumeEffectIds.Contains(effectId) || requested == 1) ? 1 : 0;

    /// <summary>The highest effect id a player can be given or ask for; the client has none past it.</summary>
    public int MaxEffectId { get; init; } = 10000;

    /// <summary>Copies of one effect a player can hold waiting to be activated.</summary>
    public int MaxCopiesPerType { get; init; } = 99;

    /// <summary>Different effects a player can own; a collection that grows per grant needs a cap.</summary>
    public int MaxDistinctEffects { get; init; } = 500;

    /// <summary>Seconds one use of the effect lasts; never below one, because the client divides by it.</summary>
    public int GetDurationSeconds(int effectId) =>
        System.Math.Max(
            1,
            DurationOverrides.TryGetValue(effectId, out var seconds)
                ? seconds
                : DefaultDurationSeconds
        );
}
