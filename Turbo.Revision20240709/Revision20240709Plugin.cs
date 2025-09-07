using System.Threading.Tasks;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Revision20240709;

public class Revision20240709Plugin(IRevisionManager revisionManager)
{
    private readonly IRevisionManager _revisionManager = revisionManager;

    public Task InitializeAsync()
    {
        var revision = new Revision20240709();
        _revisionManager.RegisterRevision(revision);

        return Task.CompletedTask;
    }
}
