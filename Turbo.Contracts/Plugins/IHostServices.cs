namespace Turbo.Contracts.Plugins;

public interface IHostServices
{
    T GetServices<T>()
        where T : notnull;
    T GetRequiredService<T>()
        where T : notnull;
}
