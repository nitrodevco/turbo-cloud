using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Grains.Observers;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Authentication;

public sealed class SessionContextObserver(SessionKey sessionKey, ISessionGateway sessionGateway)
    : ISessionContextObserver
{
    private readonly SessionKey _sessionKey = sessionKey;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        var ctx = _sessionGateway.GetSession(_sessionKey);

        if (ctx is not null)
        {
            _ = ctx.SendComposerAsync(composer, ct);
        }

        return Task.CompletedTask;
    }
}
