using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Turbo.Furniture.Abstractions;

namespace Turbo.Furniture;

public sealed class FurnitureBootstrapper(IFurnitureProvider furnitureProvider) : IHostedService
{
    private readonly IFurnitureProvider _furnitureProvider = furnitureProvider;

    public async Task StartAsync(CancellationToken ct)
    {
        await _furnitureProvider.ReloadAsync(ct).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken ct)
    {
        // Cleanup logic here
        return Task.CompletedTask;
    }
}
