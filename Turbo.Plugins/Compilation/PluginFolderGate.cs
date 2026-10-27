using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Turbo.Plugins.Compilation;

/// <summary>
/// The server runs a plugin's code only as it compiles it from the plugin's sources, so a plugin's
/// folder holds no executable file: an assembly, a native library or WebAssembly, by its
/// extension or by its first bytes (a renamed one is found too). Anything else is the plugin's
/// data (texts, JSON, images, fonts, <c>.nitro</c> bundles, its client module) and is allowed.
/// </summary>
internal static class PluginFolderGate
{
    public static readonly IReadOnlySet<string> EXTENSIONS = new HashSet<string>(
        [".dll", ".exe", ".so", ".dylib", ".wasm", ".nupkg"],
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// The executable files in <paramref name="folder"/>, each relative to it with what it is;
    /// none when the plugin may load.
    /// </summary>
    public static IReadOnlyList<string> Find(string folder)
    {
        if (!Directory.Exists(folder))
            return [];

        var found = new List<string>();

        foreach (
            var file in Directory
                .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
        )
        {
            var relative = Path.GetRelativePath(folder, file).Replace('\\', '/');

            if (EXTENSIONS.Contains(Path.GetExtension(file)))
                found.Add($"{relative} is a {Path.GetExtension(file).ToLowerInvariant()} file");
            else if (Kind(file) is { } kind)
                found.Add($"{relative} is {kind}");
        }

        return found;
    }

    /// <summary>What executable format the file's first bytes are; null for none.</summary>
    internal static string? Kind(string file)
    {
        Span<byte> head = stackalloc byte[64];
        int read;

        try
        {
            using var stream = File.OpenRead(file);

            read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);

            if (read >= 2 && head[0] == 'M' && head[1] == 'Z')
                return IsPortableExecutable(stream, head[..read])
                    ? "a Windows executable or .NET assembly"
                    : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (read < 4)
            return null;

        uint magic = (uint)(head[0] << 24 | head[1] << 16 | head[2] << 8 | head[3]);

        return magic switch
        {
            0x7F454C46 => "a Linux executable or library (ELF)",
            0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE or 0xCAFEBABE =>
                "a macOS executable or library (Mach-O)",
            0x0061736D => "WebAssembly",
            _ => null,
        };
    }

    /// <summary>
    /// An MZ header that points at a PE signature: a Windows executable or a .NET assembly. A text
    /// file that happens to start "MZ" is not one.
    /// </summary>
    private static bool IsPortableExecutable(FileStream stream, ReadOnlySpan<byte> head)
    {
        if (head.Length < 0x40)
            return false;

        var offset = BinaryPrimitives.ReadInt32LittleEndian(head[0x3C..0x40]);

        if (offset < 0x40 || offset > stream.Length - 4)
            return false;

        Span<byte> signature = stackalloc byte[4];

        stream.Position = offset;

        return stream.ReadAtLeast(signature, 4, throwOnEndOfStream: false) == 4
            && signature.SequenceEqual("PE\0\0"u8);
    }
}
