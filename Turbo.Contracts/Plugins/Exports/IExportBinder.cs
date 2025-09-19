namespace Turbo.Contracts.Plugins.Exports;

public interface IExportBinder
{
    void Export<T>(string exportKey, T instance)
        where T : class;
}
