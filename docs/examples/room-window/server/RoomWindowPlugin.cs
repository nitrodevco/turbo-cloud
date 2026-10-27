using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace RoomWindow;

/// <summary>
/// A window furni that shows the room its other half stands in, and that a player walks through
/// by clicking that room's floor. The example of the room hooks a plugin has
/// (<c>docs/examples/room-window/README.md</c>): a linked pair, a furni logic answering its
/// handlers, a room watch through the pair, a forward with an entry extension and an arrival
/// policy, and a client module that draws the far room as a secondary room and walks into it.
/// <para>
/// Everything else is found as it loads: the logic, the handlers, the arrival policy and the
/// messages in the assembly, and the client module, built by the hotel from <c>client/src</c>.
/// </para>
/// </summary>
public sealed class RoomWindowPlugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<WindowDirectory>();
}
