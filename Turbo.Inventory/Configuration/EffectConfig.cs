using System.Collections.Generic;
using Turbo.Primitives.Pets;

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

    /// <summary>
    /// Effect ids that are never a player's to own: the ones the hotel puts on avatars itself and
    /// the ones the client builds behaviour on. The room cannot tell where a worn effect came from,
    /// so taking one of the player's effects off would take the hotel's off too; and the client
    /// reads some ids to decide what an avatar is doing (swimming, riding), draws special frames
    /// for others, and applies others itself in a game. Such an id cannot be given.
    /// <list type="bullet">
    /// <item>28, 29, 30, 184, 185: water. The client animates the splash and opens its swim menu
    /// from them (<c>AvatarLogic</c>, <c>AvatarInfoWidget</c>).</item>
    /// <item>33 to 36, 38, 39: game teams, which the client draws with their own frames
    /// (<c>AvatarImage</c>).</item>
    /// <item>77: a rider (<see cref="PetRiding.RIDER_EFFECT_ID"/>).</item>
    /// <item>95, 96, 98: snow war, which the client applies itself in the arena.</item>
    /// <item>97, 218: a snowboard and a freeze, which the client draws without a shadow.</item>
    /// </list>
    /// Entries a hotel lists are added to these, never instead of them. Add any team ids it
    /// changed (<c>Turbo:Rooms:GameTeamEffectIds</c>). The wired freeze paints the effects its
    /// editor names (<c>WiredActionFreezeUser.FREEZE_EFFECT_IDS</c>).
    /// </summary>
    public HashSet<int> ReservedEffectIds { get; init; } =
    [28, 29, 30, 33, 34, 35, 36, 38, 39, PetRiding.RIDER_EFFECT_ID, 95, 96, 97, 98, 184, 185, 218];

    /// <summary>The highest effect id a player can be given or ask for; the client has none past it.</summary>
    public int MaxEffectId { get; init; } = 10000;

    /// <summary>Whether a player can be given the effect: an id from 1 to the highest, and not reserved.</summary>
    public bool CanGive(int effectId) =>
        effectId >= 1 && effectId <= MaxEffectId && !ReservedEffectIds.Contains(effectId);

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
