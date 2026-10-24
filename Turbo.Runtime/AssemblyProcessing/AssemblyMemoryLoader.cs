using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Runtime.AssemblyProcessing;

public static class AssemblyMemoryLoader
{
    public static LoadedAssembly LoadFromBytes(string mainDllPath)
    {
        if (!File.Exists(mainDllPath))
            throw new FileNotFoundException("Plugin entry DLL not found.", mainDllPath);

        var baseDir = Path.GetDirectoryName(mainDllPath)!;
        var managed = new Dictionary<string, (byte[] asm, byte[]? pdb)>(
            StringComparer.OrdinalIgnoreCase
        );

        static byte[] ReadAll(string path) => File.ReadAllBytes(path);

        foreach (var dll in Directory.EnumerateFiles(baseDir, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(dll);

            if (managed.ContainsKey(name))
                continue;

            byte[] asmBytes = ReadAll(dll);
            string pdbPath = Path.ChangeExtension(dll, ".pdb");
            byte[]? pdbBytes = File.Exists(pdbPath) ? ReadAll(pdbPath) : null;

            managed[name] = (asmBytes, pdbBytes);
        }

        var alc = new ByteLoadingAlc(baseDir, managed);
        var entryName = Path.GetFileNameWithoutExtension(mainDllPath);
        var asm = alc.LoadFromAssemblyName(new AssemblyName(entryName));

        return new LoadedAssembly(asm, alc, baseDir);
    }

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
