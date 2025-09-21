using System;
using System.Reflection;

namespace Turbo.Runtime.AssemblyProcessing;

public interface IAssemblyFeatureProcessor
{
    IDisposable Process(Assembly assembly, IServiceProvider pluginServices);
}
