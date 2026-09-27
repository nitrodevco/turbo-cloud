namespace Turbo.Inventory.Configuration;

public class InventoryConfig
{
    public const string SECTION_NAME = "Turbo:Inventory";

    /// <summary>Pets a player may own, placed or not.</summary>
    public int MaxPets { get; init; } = 300;

    /// <summary>Bots a player may own; the client drops arrivals past its own cap of 150.</summary>
    public int MaxBots { get; init; } = 150;

    /// <summary>Badges a player can wear at once; the client has five slots.</summary>
    public int MaxActiveBadges { get; init; } = 5;

    /// <summary>Items per fragment of the furni tab.</summary>
    public int FurnitureInventoryFragmentSize { get; init; } = 100;

    /// <summary>Pets per fragment of the pets tab.</summary>
    public int PetInventoryFragmentSize { get; init; } = 100;

    /// <summary>Badges per fragment of the badges tab.</summary>
    public int BadgeInventoryFragmentSize { get; init; } = 500;

    /// <summary>
    /// How many "new" items a player keeps per inventory tab. A player who never opens a tab
    /// would otherwise grow the list forever; past this, the oldest stop being new.
    /// </summary>
    public int MaxUnseenItemsPerCategory { get; init; } = 500;
    public int PetStartEnergy { get; init; } = 100;
    public int PetStartNutrition { get; init; } = 100;
    public int PetNameMinLength { get; init; } = 1;
    public int PetNameMaxLength { get; init; } = 15;

    /// <summary>Name given to a bot bought from the catalog when its product has none.</summary>
    public string BotDefaultName { get; init; } = "Bot";
    public string BotDefaultMotto { get; init; } = string.Empty;
    public int BotDefaultChatDelaySeconds { get; init; } = 7;
}
