using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Players.Grains.Respect;

public interface IHumanRespectOperationGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Completes a durable respect operation admitted only after the room verified both players
    /// are present. The grain key is the respect's id (<c>player_respects.id</c>).
    /// </summary>
    public Task<HumanRespectOperationResult> ExecuteAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    );
}
