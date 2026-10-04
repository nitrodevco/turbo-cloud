using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Admin.Permissions;

/// <summary>Builds the <see cref="PermissionEditor"/> for a staff member, from their resolved permissions and the groups now.</summary>
public sealed class PermissionEditPolicy(
    IGrainFactory grainFactory,
    IPermissionRegistryProvider registryProvider
)
{
    public async Task<PermissionEditor> ForAsync(PlayerId editor, CancellationToken ct)
    {
        var groups = await grainFactory
            .GetPermissionGroupDirectoryGrain()
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(editor)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);

        return new PermissionEditor(grainFactory, registryProvider.Current, groups, resolved);
    }
}
