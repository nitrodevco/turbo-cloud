using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

    public static async Task<bool> UnloadAndWaitAsync(
        ByteLoadingAlc alc,
        int maxMs = 5000,
        CancellationToken ct = default
    )
    {
        var wr = new WeakReference(alc);

        alc.Unload();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < maxMs && !ct.IsCancellationRequested)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!wr.IsAlive)
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

        return !wr.IsAlive;
    }
}
