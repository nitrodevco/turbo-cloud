using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Turbo.Plugins.Compilation;

/// <summary>A plugin compiled: its assembly, its symbols (with its sources in them) and its doc comments.</summary>
internal sealed record CompiledPlugin(byte[] Assembly, byte[] Symbols, byte[] Documentation);

internal enum CacheRead
{
    Hit,
    Miss,

    /// <summary>Its files were not the ones the server wrote: they were deleted.</summary>
    Tampered,
}

/// <summary>
/// The plugins the server compiled, so a restart loads them without compiling again:
/// <c>&lt;plugins&gt;/.compiled/&lt;key&gt;/&lt;hash&gt;.dll</c>, <c>.pdb</c>, <c>.xml</c> and
/// <c>.mac</c>, the newest compile of each plugin only. The hash is of everything the compile
/// read (<see cref="PluginCompiler"/>); the <c>.mac</c> is an HMAC-SHA256 of the files under a key
/// only this server has (<c>.compiled/.key</c>), so an assembly put in the cache by anyone else is
/// not loaded: it is deleted and the plugin compiled again.
/// </summary>
internal sealed partial class PluginCompileCache(string root)
{
    public const string FOLDER = ".compiled";

    private static readonly string[] EXTENSIONS = [".dll", ".pdb", ".xml", ".mac"];

    public CacheRead TryRead(string pluginKey, string hash, out CompiledPlugin? compiled)
    {
        compiled = null;

        var folder = FolderOf(pluginKey);
        var paths = EXTENSIONS.Select(ext => Path.Combine(folder, hash + ext)).ToArray();

        if (!paths.All(File.Exists))
            return CacheRead.Miss;

        try
        {
            var found = new CompiledPlugin(
                File.ReadAllBytes(paths[0]),
                File.ReadAllBytes(paths[1]),
                File.ReadAllBytes(paths[2])
            );
            var mac = File.ReadAllBytes(paths[3]);

            if (CryptographicOperations.FixedTimeEquals(mac, Mac(hash, found)))
            {
                compiled = found;

                return CacheRead.Hit;
            }
        }
        catch (IOException)
        {
            return CacheRead.Miss;
        }

        foreach (var path in paths)
            File.Delete(path);

        return CacheRead.Tampered;
    }

    /// <summary>Keeps <paramref name="compiled"/> as the plugin's one compile, its mac written last.</summary>
    public void Write(string pluginKey, string hash, CompiledPlugin compiled)
    {
        var folder = FolderOf(pluginKey);

        Directory.CreateDirectory(folder);

        foreach (var old in Directory.EnumerateFiles(folder))
            File.Delete(old);

        Save(Path.Combine(folder, hash + ".dll"), compiled.Assembly);
        Save(Path.Combine(folder, hash + ".pdb"), compiled.Symbols);
        Save(Path.Combine(folder, hash + ".xml"), compiled.Documentation);
        Save(Path.Combine(folder, hash + ".mac"), Mac(hash, compiled));
    }

    /// <summary>The doc comments file of the plugin's compile <paramref name="hash"/>.</summary>
    public string DocumentationPath(string pluginKey, string hash) =>
        Path.Combine(FolderOf(pluginKey), hash + ".xml");

    private string FolderOf(string pluginKey) =>
        Path.Combine(
            root,
            SafeName().IsMatch(pluginKey) && pluginKey is not ("." or "..")
                ? pluginKey
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pluginKey)))
        );

    private static void Save(string path, byte[] bytes)
    {
        var temp = path + ".tmp";

        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The files' HMAC, bound to the hash they are kept under.</summary>
    private byte[] Mac(string hash, CompiledPlugin compiled)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Key());

        hmac.AppendData(Encoding.UTF8.GetBytes(hash));

        foreach (var part in new[] { compiled.Assembly, compiled.Symbols, compiled.Documentation })
        {
            hmac.AppendData(BitConverter.GetBytes((long)part.Length));
            hmac.AppendData(part);
        }

        return hmac.GetHashAndReset();
    }

    private byte[]? _key;

    private byte[] Key() => _key ??= ReadOrMakeKey();

    /// <summary>This server's key, made once: 32 random bytes only its owner may read.</summary>
    private byte[] ReadOrMakeKey()
    {
        var path = Path.Combine(root, ".key");

        if (File.Exists(path) && File.ReadAllBytes(path) is { Length: 32 } key)
            return key;

        Directory.CreateDirectory(root);
        key = RandomNumberGenerator.GetBytes(32);

        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };

        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(path, options))
            stream.Write(key);

        return key;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeName();
}
