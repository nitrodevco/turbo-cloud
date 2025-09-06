using System.Threading.Tasks;
using Turbo.Core.Authorization;
using Turbo.Core.Game.Players;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Revision20240709;

public class Revision20240709Plugin(
    IRevisionManager revisionManager,
    IAuthorizationManager authorizationManager,
    IPlayerManager playerManager
)
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IAuthorizationManager _authorizationManager = authorizationManager;
    private readonly IPlayerManager _playerManager = playerManager;

    public Task InitializeAsync()
    {
        var revision = new Revision20240709();
        _revisionManager.RegisterRevision(revision);

        return Task.CompletedTask;
    }
}
