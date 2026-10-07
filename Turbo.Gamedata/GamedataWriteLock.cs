using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Gamedata;

/// <summary>
/// Gamedata writes take turns: an import, an edit and a rollback each read the definitions, then
/// write them, and two at once would each add what the other adds, or undo what it just did.
/// </summary>
internal sealed class GamedataWriteLock
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> write, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            return await write().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
