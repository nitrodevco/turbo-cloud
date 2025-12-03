using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Networking.Session;

public sealed class SessionContextObserver(SessionKey sessionKey, ISessionGateway sessionGateway)
    : ISessionContextObserver
{
    private readonly SessionKey _sessionKey = sessionKey;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct)
    {
        var ctx = _sessionGateway.GetSession(_sessionKey);

        if (ctx is not null)
        {
            await ctx.SendComposerAsync(composer, ct).ConfigureAwait(false);
        }
    }
}
