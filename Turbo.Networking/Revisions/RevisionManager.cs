using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Networking.Revisions;

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
