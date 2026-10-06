using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Assets;

namespace Turbo.Admin.Api;

/// <summary>
/// Where the client loads its images from, for any signed-in staff member: the addresses are
/// already public in the client's own config.
/// </summary>
internal sealed class ClientAssetEndpoints(ClientAssets assets)
{
    public void Map(RouteGroupBuilder secured) =>
        secured.MapGet(
            "/client/assets",
            async (CancellationToken ct) =>
                Results.Ok(await assets.GetAsync(ct).ConfigureAwait(false))
        );
}
