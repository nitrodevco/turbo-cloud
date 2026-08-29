using System;

namespace Turbo.Plugins.Exceptions;

public enum PluginAssemblyErrorType
{
    /// <summary>No assembly matching the manifest could be located in the plugin directory.</summary>
    NotFound,

    /// <summary>The assembly loaded, but contains no ITurboPlugin entry point.</summary>
    EntryPointNotFound,

    /// <summary>The entry point's key does not match the key declared in the manifest.</summary>
    KeyMismatch,
}

/// <summary>Raised when a plugin's assembly cannot be located, or does not expose a usable entry point.</summary>
public sealed class PluginAssemblyException : PluginException
{
    public PluginAssemblyErrorType ErrorType { get; }

    /// <summary>Plugin directory or assembly name involved in the failure, when known.</summary>
    public string? AssemblyLocation { get; }

    /// <summary>Key reported by the loaded entry point, set only for <see cref="PluginAssemblyErrorType.KeyMismatch"/>.</summary>
    public string? EntryPointKey { get; }

    public PluginAssemblyException(
        PluginAssemblyErrorType errorType,
        string? pluginKey = null,
        string? assemblyLocation = null,
        string? entryPointKey = null,
        Exception? innerException = null
    )
        : base(
            BuildMessage(errorType, pluginKey, assemblyLocation, entryPointKey),
            pluginKey,
            innerException
        )
    {
        ErrorType = errorType;
        AssemblyLocation = assemblyLocation;
        EntryPointKey = entryPointKey;
    }

    private static string BuildMessage(
        PluginAssemblyErrorType errorType,
        string? pluginKey,
        string? assemblyLocation,
        string? entryPointKey
    ) =>
        errorType switch
        {
            PluginAssemblyErrorType.NotFound =>
                $"No assembly found for plugin '{pluginKey}' in '{assemblyLocation}'.",
            PluginAssemblyErrorType.EntryPointNotFound =>
                $"No ITurboPlugin entry point found in assembly '{assemblyLocation}'.",
            PluginAssemblyErrorType.KeyMismatch =>
                $"Plugin key mismatch: manifest declares '{pluginKey}' but the entry point reports '{entryPointKey}'.",
            _ => $"Plugin assembly for '{pluginKey}' is not valid.",
        };
}
