using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Turbo.Runtime.AssemblyProcessing;

public sealed class ByteLoadingAlc(
    string basePath,
    IReadOnlyDictionary<string, (byte[] asm, byte[]? pdb)> managed
) : AssemblyLoadContext(isCollectible: true)
{
    private readonly IReadOnlyDictionary<string, (byte[] asm, byte[]? pdb)> _managed = managed;
    private readonly AssemblyDependencyResolver _resolver = new(basePath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simple = assemblyName.Name!;

        if (_managed.TryGetValue(simple, out var blob))
        {
            using var msAsm = new MemoryStream(blob.asm, writable: false);

            if (blob.pdb is { } pdb)
            {
                using var msPdb = new MemoryStream(pdb, writable: false);

                return LoadFromStream(msAsm, msPdb);
            }

            return LoadFromStream(msAsm);
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
            return null;

        if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var pdbPath = Path.ChangeExtension(path, ".pdb");
            var asmBytes = File.ReadAllBytes(path);
            byte[]? pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;

            using var msAsm = new MemoryStream(asmBytes, writable: false);

            if (pdbBytes is { })
            {
                using var msPdb = new MemoryStream(pdbBytes, writable: false);
                return LoadFromStream(msAsm, msPdb);
            }

            return LoadFromStream(msAsm);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (path is null)
            return IntPtr.Zero;

        return LoadUnmanagedDllFromPath(path);
    }
}
