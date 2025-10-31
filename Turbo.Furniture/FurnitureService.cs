using Microsoft.Extensions.Logging;
using Turbo.Furniture.Abstractions;

namespace Turbo.Furniture;

public sealed class FurnitureService(
    ILogger<IFurnitureService> logger,
    IFurnitureProvider furnitureProvider
) : IFurnitureService
{
    private readonly ILogger<IFurnitureService> _logger = logger;
    private readonly IFurnitureProvider _furnitureProvider = furnitureProvider;
}
