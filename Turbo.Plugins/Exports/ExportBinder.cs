using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Plugins.Exports;

internal sealed class ExportBinder(ExportRegistry registry) : IExportBinder
{
    public Task ExportAsync<T>(string exportKey, T instance)
        where T : class => registry.SwapAsync(exportKey, instance);
}
