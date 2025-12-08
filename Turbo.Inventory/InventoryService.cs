using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Inventory;

namespace Turbo.Inventory;

public sealed class InventoryService(ILogger<IInventoryService> logger, IGrainFactory grainFactory)
    : IInventoryService
{
    private readonly ILogger<IInventoryService> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;
}
