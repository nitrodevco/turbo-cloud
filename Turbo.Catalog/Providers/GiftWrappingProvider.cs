using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;

namespace Turbo.Catalog.Providers;

/// <summary>
/// Resolves the configured wrapping names to the sprite ids the client draws. Read on every
/// call, so a definitions reload is picked up; a name with no floor definition is logged and
/// left out rather than offered as a box the client cannot draw.
/// </summary>
internal sealed class GiftWrappingProvider(
    IOptions<CatalogConfig> options,
    IFurnitureDefinitionProvider definitions,
    ILogger<IGiftWrappingProvider> logger
) : IGiftWrappingProvider
{
    private readonly GiftWrappingConfig _config = options.Value.GiftWrapping;
    private readonly IFurnitureDefinitionProvider _definitions = definitions;
    private readonly ILogger<IGiftWrappingProvider> _logger = logger;

    public GiftWrappingSnapshot GetWrapping() =>
        new()
        {
            Enabled = _config.Enabled,
            Price = _config.Price,
            StuffTypes = SpriteIds(_config.WrapperNames),
            BoxTypes = [.. _config.BoxTypes],
            RibbonTypes = [.. _config.RibbonTypes],
            DefaultStuffTypes = SpriteIds(_config.DefaultNames),
        };

    private ImmutableArray<int> SpriteIds(IEnumerable<string> names) =>
        [
            .. names
                .Select(name =>
                {
                    var definition = _definitions.TryGetDefinitionByName(name);

                    if (definition is null || definition.ProductType != ProductType.Floor)
                    {
                        _logger.LogWarning(
                            "Gift wrapping {Name} has no floor furniture definition; not offered",
                            name
                        );

                        return -1;
                    }

                    return definition.SpriteId;
                })
                .Where(spriteId => spriteId >= 0),
        ];
}
