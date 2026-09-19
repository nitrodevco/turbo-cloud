using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// Reads a typed section from an item's extra data, falling back to the same section in the
/// furniture definition's extra data so a furniture type can carry a default for every item.
/// Every section record is read through here, so an unreadable section is always logged with
/// its name instead of silently turning into "absent".
/// </summary>
public static class FurnitureExtraDataSections
{
    public static TSection? Read<TSection>(
        IExtraData itemExtraData,
        string? definitionExtraData,
        string section,
        ILogger logger
    )
        where TSection : class
    {
        if (itemExtraData.TryGetSection(section, out var element))
        {
            var stored = Deserialize<TSection>(element, section, logger);

            if (stored is not null)
                return stored;
        }

        if (string.IsNullOrWhiteSpace(definitionExtraData))
            return null;

        try
        {
            using var document = JsonDocument.Parse(definitionExtraData);

            return document.RootElement.TryGetProperty(section, out var property)
                ? Deserialize<TSection>(property, section, logger)
                : null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Definition extra data is not valid JSON; section {Section} ignored",
                section
            );

            return null;
        }
    }

    /// <summary>A section only items carry (no per-type default in the definition).</summary>
    public static TSection? Read<TSection>(IExtraData itemExtraData, string section, ILogger logger)
        where TSection : class => Read<TSection>(itemExtraData, null, section, logger);

    private static TSection? Deserialize<TSection>(
        JsonElement element,
        string section,
        ILogger logger
    )
        where TSection : class
    {
        try
        {
            return element.Deserialize<TSection>();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Extra data section {Section} does not fit {SectionType}; ignored",
                section,
                typeof(TSection).Name
            );

            return null;
        }
    }
}
