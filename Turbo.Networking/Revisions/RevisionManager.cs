using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Networking.Revisions;

public class RevisionManager(ILoggerFactory loggerFactory) : IRevisionManager
{
    public IDictionary<string, IRevision> Revisions { get; } = new Dictionary<string, IRevision>();

    private readonly ILogger _logger = loggerFactory.CreateLogger(nameof(RevisionManager));

    public IRevision? GetRevision(string revisionId) =>
        Revisions.TryGetValue(revisionId, out var revision) ? revision : null;

    public void RegisterRevision(IRevision revision)
    {
        if (revision is null)
        {
            return;
        }

        _logger.LogDebug("Registering revision {Revision}", revision.Revision);

        Revisions[revision.Revision] = revision;
    }
}
