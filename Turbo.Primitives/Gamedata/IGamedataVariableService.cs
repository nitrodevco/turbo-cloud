using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The client's external variables (<see cref="GamedataFiles.EXTERNAL_VARIABLES"/>): its
/// configuration, kept by the hotel and served from the gamedata host. Staff edit, add and remove
/// them, or import a client config whole. Every change is a change set
/// (<see cref="IGamedataHistoryService"/>).
/// </summary>
public interface IGamedataVariableService
{
    /// <summary>Variables whose key or value holds the words, by key: a page of them.</summary>
    public Task<VariableSearchResult> SearchAsync(string? query, int page, CancellationToken ct);

    /// <summary>
    /// Sets a variable to a JSON value, adding it when there is none. Throws
    /// <see cref="System.ArgumentException"/> for an empty or overlong key, a value that isn't
    /// JSON, or a key the hotel writes itself.
    /// </summary>
    public Task<VariableEntrySnapshot> SaveAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Removes a variable; false when there is none.</summary>
    public Task<bool> DeleteAsync(string key, PlayerId player, CancellationToken ct);

    /// <summary>
    /// What importing the config (a JSON object, as <c>nitro-config.json</c>) would do. Throws
    /// <see cref="System.ArgumentException"/> when it isn't a JSON object.
    /// </summary>
    public Task<VariableImportPreview> PreviewImportAsync(string json, CancellationToken ct);

    /// <summary>
    /// Takes in every key of the config, added or changed; the hotel's other variables stay. Null
    /// when the hotel already had everything; otherwise the change set made.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        string json,
        PlayerId player,
        CancellationToken ct
    );
}
