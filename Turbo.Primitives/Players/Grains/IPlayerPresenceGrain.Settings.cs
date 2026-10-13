using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    /// <summary>
    /// The player changed their own word filter: the room they are standing in masks the chat it
    /// sends them with the new list. Out of a room there is nothing to tell; the next room reads
    /// the list as they enter.
    /// </summary>
    [AlwaysInterleave]
    public Task OnCustomFilterWordsChangedAsync(List<string> words, CancellationToken ct);
}
