using System.Reflection;

namespace Turbo.Runtime.AssemblyProcessing;

public sealed record LoadedAssembly(Assembly Assembly, ByteLoadingAlc Alc, string BaseDir);
