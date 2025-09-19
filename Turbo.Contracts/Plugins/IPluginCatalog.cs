using Turbo.Contracts.Plugins.Exports;

namespace Turbo.Contracts.Plugins;

public interface IPluginCatalog
{
    IExport<T> GetExport<T>(string exportKey)
        where T : class;
}
