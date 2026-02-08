using System.Collections.Generic;

namespace Turbo.Primitives.Networking.Revisions;

public interface IRevisionManager
{
    public IDictionary<string, IRevision> Revisions { get; }
    public string DefaultRevisionId { get; }

    public IRevision? GetRevision(string revisionName);

    public void RegisterRevision(IRevision revision);
}
