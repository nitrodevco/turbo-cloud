using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Runtime.AssemblyProcessing;

public static class AssemblyMemoryLoader
{
    /// <summary>
    /// A plugin's assembly, compiled by the server from its sources, in a collectible context of
    /// its own that loads nothing else (<see cref="ByteLoadingAlc"/>).
    /// </summary>
    public static LoadedAssembly Load(byte[] assembly, byte[] symbols, string baseDir)
    {
        var alc = new ByteLoadingAlc();

        using var image = new MemoryStream(assembly, writable: false);
        using var pdb = new MemoryStream(symbols, writable: false);

        return new LoadedAssembly(alc.LoadFromStream(image, pdb), alc, baseDir);
    }

    /// <summary>
    /// An assembly with no types: what a plugin with no code is loaded as, so it is held and
    /// scanned (finding nothing) as any other. It has no context to unload, so a reload does not
    /// wait for one to be collected; the one assembly of a name is kept and given again.
    /// </summary>
    public static LoadedAssembly Empty(string baseDir, string name) =>
        new(
            EMPTY.GetOrAdd(
                name,
                key =>
                    AssemblyBuilder.DefineDynamicAssembly(
                        new AssemblyName(key),
                        AssemblyBuilderAccess.Run
                    )
            ),
            null,
            baseDir
        );

    private static readonly ConcurrentDictionary<string, Assembly> EMPTY = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// Starts unloading <paramref name="alc"/> and answers a weak reference to it, for
    /// <see cref="WaitForUnloadAsync"/>. Only a weak reference may be kept past this call: a
    /// context is collected once nothing holds it or anything loaded in it, the caller's own
    /// locals and parameters included, so the caller drops what it holds of the plugin before it
    /// waits. Not inlined, so this frame's strong reference goes with it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WeakReference BeginUnload(ByteLoadingAlc alc)
    {
        alc.Unload();

        return new WeakReference(alc);
    }

    /// <summary>
    /// Collects until the context <see cref="BeginUnload"/> started unloading is gone, for at most
    /// <paramref name="maxMs"/> but at least <paramref name="minCollections"/> collections; false
    /// when something still holds it.
    /// Methods the runtime generated for the context's types (a service provider's compiled
    /// constructors, a serializer's accessors) are freed by finalizers, a step per collection, and
    /// a collection of a large heap on a busy machine can take seconds: a time alone gives up
    /// while the context is still on its way out.
    /// </summary>
    public static async Task<bool> WaitForUnloadAsync(
        WeakReference alc,
        int maxMs = 5000,
        CancellationToken ct = default,
        int minCollections = 0
    )
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var collections = 0;

        while (
            (sw.ElapsedMilliseconds < maxMs || collections < minCollections)
            && !ct.IsCancellationRequested
        )
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            collections++;

            if (!alc.IsAlive)
                return true;

            try
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return !alc.IsAlive;
    }
}
