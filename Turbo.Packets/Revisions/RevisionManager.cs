using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Incoming.Handshake;

namespace Turbo.Packets.Revisions;

public class RevisionManager(ILogger<IRevisionManager> logger) : IRevisionManager
{
    public IRevision DefaultRevision { get; private set; }
    public IDictionary<string, IRevision> Revisions { get; } = new Dictionary<string, IRevision>();
    private readonly ILogger<IRevisionManager> _logger = logger;

    public IRevision GetRevision(string revisionId)
    {
        if (string.IsNullOrWhiteSpace(revisionId)) return DefaultRevision;

        return Revisions.TryGetValue(revisionId, out var revision) ? revision : DefaultRevision;
    }

    public void RegisterRevision(IRevision revision)
    {
        if (revision is null) return;

        Revisions[revision.Revision] = revision;

        DefaultRevision = revision;
    }
}