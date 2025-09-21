using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;
using Turbo.Plugins.Exports;

namespace Turbo.Plugins;

internal sealed class PluginCatalog(ExportRegistry reg) : IPluginCatalog
{
    public IExport<T> GetExport<T>(string exportKey)
        where T : class => reg.GetOrCreate<T>(exportKey);
}
