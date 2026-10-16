using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Wired.WebApi;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// The "Variable Global Add-on: Web API" (<c>wf_xtra_var_web_api</c>): the keys its box issues and
/// the public Web API calls that read and write this room's wired variables with them.
/// </summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// A new read or write key for the Web API add-on <paramref name="boxId"/>, which the editor
    /// shows until it is saved. Null when there is no such box or the caller may not edit it.
    /// </summary>
    public Task<string?> GenerateWiredWebApiKeyAsync(
        ActionContext ctx,
        int boxId,
        bool isReadKey,
        CancellationToken ct
    );

    /// <summary>Answers one Web API call against this room's variables.</summary>
    public Task<WiredWebApiResponse> HandleWiredWebApiAsync(
        WiredWebApiRequest request,
        CancellationToken ct
    );
}
