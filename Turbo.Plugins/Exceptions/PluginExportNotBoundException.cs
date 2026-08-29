using System;

namespace Turbo.Plugins.Exceptions;

/// <summary>Raised when a reloadable export is read before any implementation has been bound to it.</summary>
public sealed class PluginExportNotBoundException(Type exportType)
    : PluginException($"Export '{exportType.Name}' has not been bound yet.")
{
    public Type ExportType { get; } = exportType;
}
