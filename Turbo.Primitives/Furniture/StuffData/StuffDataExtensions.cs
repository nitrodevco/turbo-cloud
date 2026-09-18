namespace Turbo.Primitives.Furniture.StuffData;

public static class StuffDataExtensions
{
    /// <summary>The int at <paramref name="index"/>, or 0 when the data is shorter.</summary>
    public static int ValueAt(this INumberStuffData data, int index) =>
        index >= 0 && index < data.Data.Count ? data.Data[index] : 0;

    /// <summary>The string at <paramref name="index"/>, or empty when the data is shorter.</summary>
    public static string ValueAt(this IStringStuffData data, int index) =>
        index >= 0 && index < data.Data.Count ? data.Data[index] : string.Empty;

    public static string ValueOf(this IMapStuffData data, string key) =>
        data.Data.TryGetValue(key, out var value) ? value : string.Empty;
}
