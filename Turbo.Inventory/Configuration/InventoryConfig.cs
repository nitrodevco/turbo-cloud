namespace Turbo.Inventory.Configuration;

public class InventoryConfig
{
    public const string SECTION_NAME = "Turbo:Inventory";

    public int FurniPerFragment { get; init; } = 100;
}
