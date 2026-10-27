using Turbo.Contracts.Hotel;

namespace Turbo.Plugins;

/// <summary>
/// The host's hotel API as one plugin's container is given it: what the plugin holds and has
/// watched through it is the plugin's, so the plugin's unload ends it
/// (<c>IHotelRooms.HoldAsync</c>, <c>WatchAsync</c>).
/// </summary>
public interface IPluginBoundHotel
{
    IHotel ForPlugin(string pluginKey);
}
