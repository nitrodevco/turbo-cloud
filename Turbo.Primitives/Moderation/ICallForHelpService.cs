using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Snapshots;

namespace Turbo.Primitives.Moderation;

/// <summary>Calls for help: what they can be about.</summary>
public interface ICallForHelpService
{
    /// <summary>The categories and topics, in their order; read once and held.</summary>
    Task<ImmutableArray<CfhCategorySnapshot>> GetTopicsAsync(CancellationToken ct);
}
