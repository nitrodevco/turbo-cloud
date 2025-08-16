using System.Collections.Generic;

namespace Turbo.Core.Packets.Revisions;

public interface IRevisionManager
{
    public IRevision DefaultRevision { get; }

    public IDictionary<string, IRevision> Revisions { get; }

    public IRevision GetRevision(string revisionName);

    public void RegisterRevision(IRevision revision);
}
