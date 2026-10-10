using System.Collections.Generic;
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
    /// Sets a variable to a JSON value, adding it when there is none; one that followed a setting or
    /// a file stops. Throws <see cref="System.ArgumentException"/> for an empty or overlong key, or a
    /// value that isn't JSON.
    /// </summary>
    public Task<VariableEntrySnapshot> SaveAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Links a variable to a server setting or to the address of one of the hotel's gamedata files,
    /// adding it when there is none: the file then writes the setting's value, or the file's address
    /// by hash, and is built again whenever it changes. Neither unlinks it, keeping the value it had
    /// (a file's, its address that never changes). Throws <see cref="System.ArgumentException"/> for
    /// both at once, an unknown or secret setting, a file no variable can follow, or unlinking a
    /// variable there isn't.
    /// </summary>
    public Task<VariableEntrySnapshot> LinkAsync(
        string key,
        string? settingPath,
        string? file,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// The variables that follow each file's address, by file: every file a variable can follow,
    /// with no keys for one nothing follows.
    /// </summary>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetFileKeysAsync(
        CancellationToken ct
    );

    /// <summary>
    /// Makes the variable under this key the one that carries the file's address: it follows the
    /// file (added when there is none), and every other variable that did is unlinked, keeping the
    /// file's address that never changes. One change set. Throws
    /// <see cref="System.ArgumentException"/> for an empty or overlong key, or a file no variable
    /// can follow.
    /// </summary>
    public Task<VariableEntrySnapshot> SetFileKeyAsync(
        string file,
        string key,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Removes a variable; false when there is none.</summary>
    public Task<bool> DeleteAsync(string key, PlayerId player, CancellationToken ct);

    /// <summary>
    /// What importing the config (a JSON object, as <c>nitro-config.json</c>) would do, removing the
    /// variables it lacks when asked. Throws <see cref="System.ArgumentException"/> when it isn't a
    /// JSON object.
    /// </summary>
    public Task<VariableImportPreview> PreviewImportAsync(
        string json,
        bool removeMissing,
        CancellationToken ct
    );

    /// <summary>
    /// Takes in every key of the config, added or changed. The hotel's other variables stay, or with
    /// <paramref name="removeMissing"/> are removed; a variable that follows a setting or a file
    /// stays either way. Null when the hotel already had everything; otherwise the change set made.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        string json,
        bool removeMissing,
        PlayerId player,
        CancellationToken ct
    );
}
