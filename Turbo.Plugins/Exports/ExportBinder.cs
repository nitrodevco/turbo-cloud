using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Plugins.Exports;

public sealed class ExportBinder(ExportRegistry registry) : IExportBinder
{
    public void Export<T>(string exportKey, T instance)
        where T : class => registry.Swap(exportKey, instance);
}
