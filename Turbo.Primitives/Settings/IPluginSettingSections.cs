using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Settings;

/// <summary>
/// The settings of the plugins live now. A plugin's config classes (those it binds with
/// <c>services.AddOptions&lt;T&gt;().BindConfiguration(section)</c>, naming the section in a
/// <c>SECTION_NAME</c> constant) are listed, saved and put back in the admin panel like the host's
/// for as long as the plugin is loaded.
/// </summary>
public interface IPluginSettingSections
{
    /// <summary>
    /// Adds the sections of <paramref name="types"/> as the plugin's; disposing the result takes
    /// them away again. <paramref name="running"/> gives the plugin's value of a class now;
    /// <paramref name="docsFile"/> is the XML documentation the summaries are read from, if any.
    /// Throws <see cref="ArgumentException"/> when a section or setting is already the host's or
    /// another plugin's.
    /// </summary>
    public IDisposable Add(
        string plugin,
        IReadOnlyList<Type> types,
        Func<Type, object?> running,
        string? docsFile
    );
}
