namespace Turbo.Primitives.Furniture;

/// <summary>
/// A clothing booth keeps one look per gender in its legacy data, as the client's
/// <c>FurnitureClothingChangeLogic</c> reads it: "boy figure,girl figure", either side empty
/// until it has been dressed.
/// </summary>
public static class ClothingChangeData
{
    public const char SEPARATOR = ',';

    public static (string boy, string girl) Parse(string? data)
    {
        var parts = (data ?? string.Empty).Split(SEPARATOR);

        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    public static string Compose(string boy, string girl) => $"{boy}{SEPARATOR}{girl}";
}
