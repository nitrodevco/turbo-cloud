namespace Turbo.Plugins.Utilities;

internal interface IPluginOptionsSnapshot<T>
    where T : class
{
    T Value { get; }
}
