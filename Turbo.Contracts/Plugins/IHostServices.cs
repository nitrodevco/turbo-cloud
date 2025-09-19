namespace Turbo.Contracts.Plugins;

public interface IHostServices
{
    T GetRequiredService<T>()
        where T : notnull;
}
