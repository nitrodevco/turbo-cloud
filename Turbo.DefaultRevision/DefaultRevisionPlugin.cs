using System.Threading.Tasks;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.DefaultRevision;

public class DefaultRevisionPlugin(IRevisionManager revisionManager)
{
    private readonly IRevisionManager _revisionManager = revisionManager;

    public Task InitializeAsync()
    {
        var revision = new RevisionDefault();
        _revisionManager.RegisterRevision(revision);

        return Task.CompletedTask;
    }
}
