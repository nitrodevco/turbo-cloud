using System.Reflection;

namespace Turbo.Runtime.AssemblyProcessing;

/// <summary>A plugin's assembly and the context it unloads with; a plugin with no code has none.</summary>
public sealed record LoadedAssembly(Assembly Assembly, ByteLoadingAlc? Alc, string BaseDir);
