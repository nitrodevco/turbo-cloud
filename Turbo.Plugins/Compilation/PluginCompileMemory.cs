using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Plugins.Exceptions;

namespace Turbo.Plugins.Compilation;

/// <summary>
/// Compiles kept in memory for the life of the process, shared by every plugin host given this
/// one: a test run's test hotels (Turbo.Testing's <c>TestHotel</c>, docs/plugin-testing.md),
/// where each hotel is a new folder whose disk cache (<see cref="PluginCompileCache"/>) starts
/// empty. A server has none. Keyed as the disk cache is, by everything the compile read, so
/// sources changed in any way compile again. A refusal or a compile error is kept as well: the
/// same sources fail the same way. Only the bytes are shared; each host loads its own assembly.
/// </summary>
internal sealed class PluginCompileMemory
{
    private readonly ConcurrentDictionary<string, Task<CompiledPlugin>> _compiles = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// The compile of <paramref name="hash"/>: one already made (or being made) for another host,
    /// else <paramref name="compile"/>'s, which runs once for everyone waiting on it. A failure
    /// other than the plugin's own (refused, does not compile) is not kept.
    /// </summary>
    public async Task<(CompiledPlugin Compiled, bool Shared)> GetOrCompileAsync(
        string hash,
        Func<Task<CompiledPlugin>> compile,
        CancellationToken ct
    )
    {
        var mine = new TaskCompletionSource<CompiledPlugin>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var kept = _compiles.GetOrAdd(hash, mine.Task);
        var shared = kept != mine.Task;

        if (!shared)
        {
            try
            {
                mine.SetResult(await compile().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                mine.SetException(ex);
            }
        }

        try
        {
            return (await kept.WaitAsync(ct).ConfigureAwait(false), shared);
        }
        catch (Exception ex)
            when (ex is not (PluginRejectedException or PluginCompileException) && kept.IsCompleted)
        {
            _compiles.TryRemove(KeyValuePair.Create(hash, kept));

            throw;
        }
    }
}
