using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Quests.RewardTracks;

/// <summary>
/// A prize. <see cref="ProductType"/> and <see cref="RewardTypeId"/> are what the client draws
/// (a point type, a badge code, a furni's sprite id, a Habbicon id, a pet type); a furni prize
/// also names the furniture definition to give in <see cref="FurnitureDefinitionId"/>. A pet's
/// <see cref="ExtraParams"/> is its figure (type, palette and colour), a bot's its look.
/// </summary>
public sealed class RewardTrackPrizeDefinition
{
    public string Id { get; set; } = "";

    public int RequiredPoints { get; set; }

    public ProductDisplayType ProductType { get; set; } = ProductDisplayType.ActivityPoints;

    public string RewardTypeId { get; set; } = "";

    public string ExtraParams { get; set; } = "";

    public int Amount { get; set; } = 1;

    public bool Premium { get; set; }

    public int? FurnitureDefinitionId { get; set; }

    /// <summary>
    /// The name a pet or bot prize is given; empty for the hotel's <c>PetPrizeName</c> or
    /// <c>BotPrizeName</c> (<c>Turbo:RewardTracks</c>). Habbo's prizes carry none.
    /// </summary>
    public string Name { get; set; } = "";
}
