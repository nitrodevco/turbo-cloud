using Turbo.Primitives.Quests.Enums;

namespace Turbo.Achievements.Configuration;

/// <summary>
/// A prize. <see cref="ProductType"/> and <see cref="RewardTypeId"/> are what the client draws
/// (a point type, a badge code, a furni's sprite id); a furni prize also names the furniture
/// definition to give in <see cref="FurnitureDefinitionId"/>.
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
}
