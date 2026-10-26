using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Assets;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The client's window index and skin map (<see cref="ClientWindows"/>) for the panel's windows
/// browser, for staff with <c>admin.plugins.view</c> - the theme authors. Each is the client's
/// own JSON; <c>?fresh=true</c> reads it again rather than the kept copy. 404 with the reason when
/// the client page isn't set or the client serves no such file.
/// </summary>
internal sealed class ClientWindowEndpoints(ClientWindows windows)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/client/windows")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS);

        group.MapGet(
            "/index",
            (bool? fresh, CancellationToken ct) => FileAsync(ClientWindows.INDEX, fresh, ct)
        );
        group.MapGet(
            "/skins",
            (bool? fresh, CancellationToken ct) => FileAsync(ClientWindows.SKINS, fresh, ct)
        );
    }

    private async Task<IResult> FileAsync(string file, bool? fresh, CancellationToken ct)
    {
        var read = await windows.GetAsync(file, fresh ?? false, ct).ConfigureAwait(false);

        return read.Json is { } json
            ? Results.Bytes(json, "application/json")
            : AdminResults.Error(StatusCodes.Status404NotFound, read.Problem ?? file);
    }
}
