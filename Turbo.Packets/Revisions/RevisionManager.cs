using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming.Handshake;

namespace Turbo.Packets.Revisions;

public class RevisionManager(ILogger<IRevisionManager> logger) : IRevisionManager
{
    public IDictionary<string, IRevision> Revisions { get; } = new Dictionary<string, IRevision>();

    private readonly ILogger<IRevisionManager> _logger = logger;

    public IRevision GetRevision(string revisionId) =>
        Revisions.TryGetValue(revisionId, out var revision) ? revision : null;

    public void RegisterRevision(IRevision revision)
    {
        if (revision is null)
        {
            return;
        }

        Revisions[revision.Revision] = revision;
    }
}
