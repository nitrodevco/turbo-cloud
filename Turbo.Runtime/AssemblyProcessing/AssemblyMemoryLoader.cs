using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Turbo.Runtime.AssemblyProcessing;

public static class AssemblyMemoryLoader
{
    public sealed record LoadedAssembly(Assembly Assembly, ByteLoadingAlc Alc, string BaseDir);

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

    public static bool UnloadAndWait(ByteLoadingAlc alc, int maxMs = 5000)
    {
        var wr = new WeakReference(alc);

        alc.Unload();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        do
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!wr.IsAlive)
                return true;

            System.Threading.Thread.Sleep(50);
        } while (sw.ElapsedMilliseconds < maxMs);

        return !wr.IsAlive;
    }
}
