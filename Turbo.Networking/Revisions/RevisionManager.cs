using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Networking.Revisions;

namespace Turbo.Networking.Revisions;

public sealed class RevisionManager(ILogger<RevisionManager> logger) : IRevisionManager
{
    private readonly ILogger<RevisionManager> _logger = logger;

    // Every packet in and out looks its session's revision up, and a server runs one revision in
    // practice, so the last answer is checked with an ordinal compare before hashing the id.
    private LastLookup? _lastLookup;

    public IDictionary<string, IRevision> Revisions { get; } = new Dictionary<string, IRevision>();
    public string DefaultRevisionId { get; private set; } = string.Empty;

    public IRevision? GetRevision(string revisionId)
    {
        var last = Volatile.Read(ref _lastLookup);

        if (
            last is not null
            && string.Equals(last.RevisionId, revisionId, StringComparison.Ordinal)
        )
            return last.Revision;

        if (!Revisions.TryGetValue(revisionId, out var revision))
            return null;

        Volatile.Write(ref _lastLookup, new LastLookup(revisionId, revision));

        return revision;
    }

    public void RegisterRevision(IRevision revision)
    {
        if (revision is null)
        {
            return;
        }

        _logger.LogInformation("Revision Registered: {Revision}", revision.Revision);

        Revisions[revision.Revision] = revision;
        Volatile.Write(ref _lastLookup, null);

        if (string.IsNullOrEmpty(DefaultRevisionId))
        {
            DefaultRevisionId = revision.Revision;
        }
    }

    private sealed record LastLookup(string RevisionId, IRevision Revision);
}
