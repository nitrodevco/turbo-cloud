using System.Threading;
using System.Threading.Tasks;
using Turbo.Events.Registry;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Events;

namespace Turbo.Commands;

/// <summary>
/// Sends a player a new command tree when a permission change changed what they may use, and
/// only then: most changes (a perk, a limit, a room node) leave the commands as they were.
/// </summary>
public sealed class CommandTreePermissionsHandler(
    ICommandRegistryProvider registryProvider,
    ICommandTreeService treeService
) : IEventHandler<PlayerPermissionsChangedEvent>
{
    public async ValueTask HandleAsync(
        PlayerPermissionsChangedEvent env,
        EventContext ctx,
        CancellationToken ct
    )
    {
        var registry = registryProvider.Current;

        if (
            CommandTreeBuilder.Key(registry, env.Previous)
            == CommandTreeBuilder.Key(registry, env.Current)
        )
            return;

        if (treeService is CommandTreeService service)
            await service.SendAsync(env.PlayerId, env.Current, ct);
        else
            await treeService.SendAsync(env.PlayerId, ct);
    }
}
