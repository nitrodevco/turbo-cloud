using System;

namespace Turbo.Plugins.Exceptions;

public enum PluginManifestErrorType
{
    /// <summary>No manifest.json exists at the expected path.</summary>
    NotFound,

    /// <summary>The manifest exists but could not be read or deserialized.</summary>
    Unreadable,

    /// <summary>The manifest parsed, but a required field was absent or blank.</summary>
    MissingField,
}

/// <summary>Raised when a plugin's manifest.json is absent, unreadable or incomplete.</summary>
public sealed class PluginManifestException : PluginException
{
    public PluginManifestErrorType ErrorType { get; }

    /// <summary>Path to the manifest, or to the plugin directory when the manifest is missing.</summary>
    public string ManifestPath { get; }

    /// <summary>The offending manifest field, set only for <see cref="PluginManifestErrorType.MissingField"/>.</summary>
    public string? FieldName { get; }

    public PluginManifestException(
        PluginManifestErrorType errorType,
        string manifestPath,
        string? fieldName = null,
        Exception? innerException = null
    )
        : base(BuildMessage(errorType, manifestPath, fieldName), null, innerException)
    {
        ErrorType = errorType;
        ManifestPath = manifestPath;
        FieldName = fieldName;
    }

    private static string BuildMessage(
        PluginManifestErrorType errorType,
        string manifestPath,
        string? fieldName
    ) =>
        errorType switch
        {
            PluginManifestErrorType.NotFound => $"Plugin manifest not found at '{manifestPath}'.",
            PluginManifestErrorType.Unreadable =>
                $"Plugin manifest at '{manifestPath}' could not be read.",
            PluginManifestErrorType.MissingField =>
                $"Plugin manifest at '{manifestPath}' is missing required field '{fieldName}'.",
            _ => $"Plugin manifest at '{manifestPath}' is not valid.",
        };
}
