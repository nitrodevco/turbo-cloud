using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task OnCustomFilterWordsChangedAsync(List<string> words, CancellationToken ct)
    {
        if (_state.ActiveRoomId <= 0)
            return Task.CompletedTask;

        // Told, never awaited, as the Habbo Club is: the settings grain is waiting on this call.
        _grainFactory
            .GetRoomGrain(_state.ActiveRoomId)
            .SetPlayerCustomFilterWordsAsync(_state.PlayerId, words, CancellationToken.None)
            .LogAndForget(
                _logger,
                "tell room {RoomId} about the word filter of player {PlayerId}",
                _state.ActiveRoomId,
                _state.PlayerId
            );

        return Task.CompletedTask;
    }
}
