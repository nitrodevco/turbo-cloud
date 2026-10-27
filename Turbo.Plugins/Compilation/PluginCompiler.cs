using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins;
using Turbo.Plugins.Exceptions;
using Turbo.Plugins.Policy;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Plugins.Compilation;

/// <summary>A plugin's code as loaded, and its doc comments file (its settings' summaries).</summary>
internal sealed record PluginCode(LoadedAssembly Assembly, string? DocumentationPath);

/// <summary>
/// Compiles a plugin from its C# sources and loads it: the only way the server runs a plugin's
/// code (docs/plugin-loading.md). In order:
/// <list type="number">
/// <item>its folder holds no executable file (<see cref="PluginFolderGate"/>), else it is
/// refused;</item>
/// <item>its sources are the files its manifest's <c>Sources</c> match
/// (<see cref="PluginSourceFiles"/>); none is a data plugin, which runs no code;</item>
/// <item>a compile of the same sources, references, generators and policy is taken from the
/// cache (<see cref="PluginCompileCache"/>);</item>
/// <item>otherwise they are compiled against <see cref="PluginReferences"/> with
/// <see cref="PluginGenerators"/> (nullable on, Release, no unsafe code, the server's C#
/// version), the policy analyzer refuses what plugins may not do, and the assembly, its symbols
/// (sources embedded, so a stack trace names the plugin's file and line) and its doc comments are
/// kept.</item>
/// </list>
/// A host given a <see cref="PluginCompileMemory"/> (a test run) takes a compile of the same
/// sources made for another host from it, so each set of sources compiles once a process.
/// </summary>
internal sealed class PluginCompiler(
    string pluginFolderPath,
    ILogger logger,
    PluginCompileMemory? memory = null
)
{
    /// <summary>The C# the server itself is built with.</summary>
    public const LanguageVersion LANGUAGE = LanguageVersion.CSharp14;

    /// <summary>Goes up when what a compile makes changes, so every plugin is compiled again.</summary>
    private const string FORMAT = "turbo-plugin-compile/1";

    private static readonly CSharpParseOptions PARSE = new(
        LANGUAGE,
        DocumentationMode.Diagnose,
        SourceCodeKind.Regular
    );

    private readonly PluginCompileCache _cache = new(
        Path.Combine(pluginFolderPath, PluginCompileCache.FOLDER)
    );

    /// <summary>The name of the assembly a plugin's sources compile to.</summary>
    public static string AssemblyNameOf(PluginManifest manifest) => $"TurboPlugin.{manifest.Key}";

    /// <summary>
    /// The plugin's code, loaded in a context of its own; an empty assembly for a data plugin.
    /// Throws <see cref="PluginRejectedException"/> for a plugin the server refuses and
    /// <see cref="PluginCompileException"/> for one that does not compile. Given
    /// <paramref name="run"/> (a test's own build of the same sources), the sources are compiled
    /// and held to the policy all the same, and <paramref name="run"/> is the code, the compiled
    /// assembly never loaded.
    /// </summary>
    public async Task<PluginCode> LoadAsync(
        PluginManifest manifest,
        string folder,
        System.Reflection.Assembly? run,
        CancellationToken ct
    )
    {
        var sw = Stopwatch.StartNew();

        if (PluginFolderGate.Find(folder) is { Count: > 0 } executables)
            throw new PluginRejectedException(
                manifest.Key,
                [
                    .. executables.Select(file =>
                        $"{file}: a plugin's folder holds no executable file, the server compiles its code from its sources"
                    ),
                ]
            );

        IReadOnlyList<string> sources;

        try
        {
            sources = PluginSourceFiles.Find(folder, manifest);
        }
        catch (ArgumentException ex)
        {
            throw new PluginRejectedException(manifest.Key, [ex.Message]);
        }

        // Code its Sources do not reach is a mistake, never a data plugin without its commands.
        if (
            sources.Count == 0
            && PluginSourceFiles.UnreachedCode(folder, manifest) is { } unreached
        )
            throw new PluginRejectedException(manifest.Key, [unreached]);

        if (sources.Count == 0)
            return new PluginCode(
                run is null
                    ? AssemblyMemoryLoader.Empty(folder, $"TurboDataPlugin.{manifest.Key}")
                    : new LoadedAssembly(run, null, folder),
                null
            );

        var files = sources
            .Select(path => (Path: path, Bytes: File.ReadAllBytes(Path.Combine(folder, path))))
            .ToList();
        var hash = HashOf(manifest, files);
        var read = _cache.TryRead(manifest.Key, hash, out var compiled);

        if (read == CacheRead.Tampered)
            logger.LogWarning(
                "The compiled {Key} in the cache is not the one this server wrote: it was deleted and the plugin is compiled again",
                manifest.Key
            );

        if (compiled is null && memory is not null)
        {
            // Compiled once for everyone waiting, so not stopped by one of them giving up.
            (compiled, var shared) = await memory
                .GetOrCompileAsync(
                    hash,
                    () =>
                        Task.Run(() =>
                            CompileAsync(manifest, folder, files, CancellationToken.None)
                        ),
                    ct
                )
                .ConfigureAwait(false);
            _cache.Write(manifest.Key, hash, compiled);

            if (shared)
                logger.LogDebug(
                    "Plugin {Key} was compiled before in this process: its assembly is that compile ({Ms} ms)",
                    manifest.Key,
                    sw.ElapsedMilliseconds
                );
            else
                LogCompiled(manifest, files.Count, sw);
        }
        else if (compiled is null)
        {
            compiled = await Task.Run(() => CompileAsync(manifest, folder, files, ct), ct)
                .ConfigureAwait(false);
            _cache.Write(manifest.Key, hash, compiled);
            LogCompiled(manifest, files.Count, sw);
        }
        else
            logger.LogDebug(
                "Plugin {Key} was compiled before: its assembly is from the cache ({Ms} ms)",
                manifest.Key,
                sw.ElapsedMilliseconds
            );

        return new PluginCode(
            run is null
                ? AssemblyMemoryLoader.Load(compiled.Assembly, compiled.Symbols, folder)
                : new LoadedAssembly(run, null, folder),
            _cache.DocumentationPath(manifest.Key, hash)
        );
    }

    private void LogCompiled(PluginManifest manifest, int files, Stopwatch sw) =>
        logger.LogInformation(
            "Compiled plugin {Key} from {Files} source files in {Ms} ms",
            manifest.Key,
            files,
            sw.ElapsedMilliseconds
        );

    /// <summary>Everything a compile reads, hashed: the cache's key.</summary>
    private static string HashOf(
        PluginManifest manifest,
        IReadOnlyList<(string Path, byte[] Bytes)> files
    )
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        void Add(string text) => sha.AppendData(Encoding.UTF8.GetBytes(text + "\n"));

        Add(FORMAT);
        Add(LANGUAGE.ToString());
        Add(AssemblyNameOf(manifest));
        Add(PluginReferences.Hash);
        Add(PluginGenerators.Hash);
        Add(PluginPolicyAnalyzer.VERSION.ToString(CultureInfo.InvariantCulture));

        foreach (var (path, bytes) in files)
        {
            Add(path);
            Add(bytes.Length.ToString(CultureInfo.InvariantCulture));
            sha.AppendData(bytes);
        }

        return Convert.ToHexString(sha.GetHashAndReset())[..32];
    }

    private static async Task<CompiledPlugin> CompileAsync(
        PluginManifest manifest,
        string folder,
        IReadOnlyList<(string Path, byte[] Bytes)> files,
        CancellationToken ct
    )
    {
        var trees = new List<SyntaxTree>(files.Count);
        var unreadable = new List<string>();

        foreach (var (path, bytes) in files)
        {
            try
            {
                trees.Add(
                    CSharpSyntaxTree.ParseText(
                        SourceText.From(
                            bytes,
                            bytes.Length,
                            Encoding.UTF8,
                            SourceHashAlgorithm.Sha256,
                            throwIfBinaryDetected: true,
                            canBeEmbedded: true
                        ),
                        PARSE,
                        Path.Combine(folder, path),
                        ct
                    )
                );
            }
            catch (InvalidDataException)
            {
                unreadable.Add($"{path}: is not text");
            }
        }

        if (unreadable.Count > 0)
            throw new PluginCompileException(manifest.Key, unreadable);

        var compilation = CSharpCompilation.Create(
            AssemblyNameOf(manifest),
            trees,
            PluginReferences.All,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: false,
                deterministic: true,
                concurrentBuild: true,
                specificDiagnosticOptions:
                [
                    // A reference of a newer version than another asks for: the host's, as in its build.
                    new("CS1701", ReportDiagnostic.Suppress),
                    new("CS1702", ReportDiagnostic.Suppress),
                    // A public member without a doc comment.
                    new("CS1591", ReportDiagnostic.Suppress),
                ]
            )
        );
        var options = new BuildOptions(PluginGenerators.BUILD_PROPERTIES);
        _ = CSharpGeneratorDriver
            .Create(PluginGenerators.All, [], PARSE, options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var full, out var generated, ct);
        var own = trees.ToHashSet();

        // The policy first: a plugin that breaks it is refused whether or not it compiles.
        var policy = await full.WithAnalyzers(
                [new PluginPolicyAnalyzer()],
                new AnalyzerOptions([], options)
            )
            .GetAnalyzerDiagnosticsAsync(ct)
            .ConfigureAwait(false);
        var broken = policy
            .Where(d =>
                d.Severity == DiagnosticSeverity.Error
                && d.Location.SourceTree is { } tree
                && own.Contains(tree)
            )
            .OrderBy(d => d.Location.SourceTree!.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Location.SourceSpan.Start)
            .Select(d => Format(d, folder))
            .ToList();

        if (broken.Count > 0)
            throw new PluginRejectedException(manifest.Key, broken);

        using var assembly = new MemoryStream();
        using var symbols = new MemoryStream();
        using var documentation = new MemoryStream();
        var emitted = full.Emit(
            assembly,
            symbols,
            documentation,
            options: new EmitOptions(
                debugInformationFormat: DebugInformationFormat.PortablePdb,
                pdbFilePath: AssemblyNameOf(manifest) + ".pdb"
            ),
            embeddedTexts: full.SyntaxTrees.Select(tree => (tree, Text: tree.GetText(ct)))
                .Where(x => x.tree.FilePath.Length > 0 && x.Text.CanBeEmbedded)
                .Select(x => EmbeddedText.FromSource(x.tree.FilePath, x.Text)),
            cancellationToken: ct
        );
        var errors = generated
            .Concat(emitted.Diagnostics)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => Format(d, folder))
            .Distinct()
            .ToList();

        if (!emitted.Success || errors.Count > 0)
            throw new PluginCompileException(
                manifest.Key,
                errors.Count > 0 ? errors : ["The compiler gave no assembly."]
            );

        return new CompiledPlugin(assembly.ToArray(), symbols.ToArray(), documentation.ToArray());
    }

    /// <summary>
    /// A diagnostic as its author reads it: <c>server/SkillBook.cs(42,9): CS0103 The name ...</c>,
    /// the file relative to the plugin's folder.
    /// </summary>
    internal static string Format(Diagnostic diagnostic, string folder)
    {
        var message = $"{diagnostic.Id} {diagnostic.GetMessage(CultureInfo.InvariantCulture)}";

        if (!diagnostic.Location.IsInSource)
            return message;

        var span = diagnostic.Location.GetLineSpan();
        var path = Path.IsPathRooted(span.Path)
            ? Path.GetRelativePath(folder, span.Path).Replace('\\', '/')
            : span.Path.Replace('\\', '/');

        return $"{path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): {message}";
    }

    /// <summary>The build's properties, as MSBuild hands them to generators and analyzers.</summary>
    private sealed class BuildOptions(ImmutableDictionary<string, string> properties)
        : AnalyzerConfigOptionsProvider
    {
        private static readonly Options NONE = new(ImmutableDictionary<string, string>.Empty);

        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(properties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => NONE;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => NONE;

        private sealed class Options(ImmutableDictionary<string, string> values)
            : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value) =>
                values.TryGetValue(key, out value!);
        }
    }
}
