namespace Turbo.Contracts.Plugins.Admin;

/// <summary>
/// What a plugin adds to the admin panel, registered in its own container: its permission nodes,
/// the endpoints the host serves for it under <c>/api/plugins/&lt;key&gt;/api/</c>, and the pages
/// the panel draws with its own components from their schema (no plugin code runs in the panel).
/// Read once per load; a reload replaces all of it.
/// </summary>
public interface IPluginAdmin
{
    void Configure(IPluginAdminBuilder admin);
}
