using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Networking.Revisions;

public sealed class RevisionManager(ILogger<RevisionManager> logger) : IRevisionManager
{
    private readonly ILogger<RevisionManager> _logger = logger;
    public IDictionary<string, IRevision> Revisions { get; } = new Dictionary<string, IRevision>();

    public IRevision? GetRevision(string revisionId) =>
        Revisions.TryGetValue(revisionId, out var revision) ? revision : null;

    public void RegisterRevision(IRevision revision)
    {
        if (revision is null)
        {
            return;
        }

        _logger.LogInformation("Revision Registered: {Revision}", revision.Revision);

        Revisions[revision.Revision] = revision;
    }
}
