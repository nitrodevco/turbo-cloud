using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins.Admin;

/// <summary>Collects one load's admin extensions (<see cref="IPluginAdmin"/>).</summary>
public interface IPluginAdminBuilder
{
    /// <summary>
    /// Declares a permission node, <c>plugin.&lt;key&gt;.&lt;name&gt;</c>, and returns its full name.
    /// It is registered while the plugin is loaded and granted to no one by default.
    /// </summary>
    string Node(string name, string description);

    /// <summary>
    /// Serves <paramref name="method"/> <paramref name="pattern"/> (relative, <c>reports/{id}</c>)
    /// to staff holding <paramref name="node"/> (a full name from <see cref="Node"/>). The host
    /// signs the caller in, checks the node, caps the body and the time, audits every call but a
    /// GET, and turns an exception into an error answer.
    /// </summary>
    void Map(
        string method,
        string pattern,
        string node,
        Func<PluginAdminRequest, CancellationToken, Task<PluginAdminResult>> handler
    );

    /// <summary>A page of the panel, drawn from its schema; in the navigation when <see cref="PluginAdminPage.Nav"/>.</summary>
    void Page(PluginAdminPage page);
}
