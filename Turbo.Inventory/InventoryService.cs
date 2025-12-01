using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Orleans.Grains;

namespace Turbo.Inventory;

public sealed class InventoryService(ILogger<IInventoryService> logger, IGrainFactory grainFactory)
    : IInventoryService
{
    private readonly ILogger<IInventoryService> _logger = logger;
    private readonly IGrainFactory _grainFactory = grainFactory;
}
