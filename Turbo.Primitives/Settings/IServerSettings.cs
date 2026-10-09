using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Settings.Snapshots;

namespace Turbo.Primitives.Settings;

/// <summary>
/// The server's settings - every option of every config section the host registers - with where
/// each value comes from, and the admin panel's overrides of them. An override sits above
/// <c>appsettings.json</c> and below the environment and the command line, so an operator always
/// has the last word. Most settings apply after a restart: the server reads its options as it
/// starts.
/// </summary>
public interface IServerSettings
{
    /// <summary>
    /// Goes up every time a value configured changes (an override set or put back), so what is
    /// built from settings knows to build again.
    /// </summary>
    public int Version { get; }

    /// <summary>Every setting, by path.</summary>
    public ImmutableArray<ServerSettingSnapshot> List();

    /// <summary>The setting at the path, its case ignored; null when there is none.</summary>
    public ServerSettingSnapshot? Get(string path);

    /// <summary>
    /// The value configured now, as JSON, for what reads a setting by its path (an external
    /// variable linked to one). Null for an unknown path, and for a secret, which is never read out.
    /// </summary>
    public string? GetValue(string path);

    /// <summary>
    /// Overrides the setting with a JSON value. Throws <see cref="System.ArgumentException"/> for an
    /// unknown path, a startup setting, one the environment sets, or a value of the wrong kind.
    /// </summary>
    public Task<ServerSettingSnapshot> SaveAsync(
        string path,
        string value,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Removes the panel's override, so the files' value applies; false when there was none.</summary>
    public Task<bool> ResetAsync(string path, PlayerId player, CancellationToken ct);

    /// <summary>A page of the changes made in the panel, newest first.</summary>
    public Task<ServerSettingHistoryPage> HistoryAsync(int page, CancellationToken ct);
}
