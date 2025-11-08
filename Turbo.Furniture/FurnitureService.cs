using Microsoft.Extensions.Logging;
using Turbo.Furniture.Abstractions;

namespace Turbo.Furniture;

public sealed class FurnitureService(
    ILogger<IFurnitureService> logger,
    IFurnitureDefinitionProvider furnitureProvider
) : IFurnitureService
{
    private readonly ILogger<IFurnitureService> _logger = logger;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;
}
