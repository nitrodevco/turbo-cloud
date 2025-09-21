using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Exports;

public interface IExportBinder
{
    Task ExportAsync<T>(string exportKey, T instance)
        where T : class;
}
