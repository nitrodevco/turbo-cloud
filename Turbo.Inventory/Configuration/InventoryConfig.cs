namespace Turbo.Inventory.Configuration;

public class InventoryConfig
{
    public const string SECTION_NAME = "Turbo:Inventory";

    /// <summary>Pets a player may own, placed or not.</summary>
    public int MaxPets { get; init; } = 300;

    /// <summary>Bots a player may own; the client drops arrivals past its own cap of 150.</summary>
    public int MaxBots { get; init; } = 150;
    public int PetStartEnergy { get; init; } = 100;
    public int PetStartNutrition { get; init; } = 100;
    public int PetNameMinLength { get; init; } = 1;
    public int PetNameMaxLength { get; init; } = 15;

    /// <summary>Name given to a bot bought from the catalog when its product has none.</summary>
    public string BotDefaultName { get; init; } = "Bot";
    public string BotDefaultMotto { get; init; } = string.Empty;
    public int BotDefaultChatDelaySeconds { get; init; } = 7;
}
