using Turbo.Primitives.Settings;

namespace Turbo.Database.Configuration;

/// <summary>
/// Recording asset movements (furni, pets, bots and currencies changing owner, place or
/// existence) into <c>asset_movement_outbox</c>, for a plugin to consume. See
/// <c>docs/movements.md</c>.
/// </summary>
public class MovementsConfig
{
    public const string SECTION_NAME = "Turbo:Movements";

    /// <summary>
    /// Whether movements are recorded. Off, nothing is written and nothing costs more than
    /// reading this; turning it on or off writes a <c>recording.started</c> or
    /// <c>recording.stopped</c> row, so a consumer can tell "nothing happened" from "nothing was
    /// recorded".
    /// </summary>
    [LiveSetting]
    public bool Enabled { get; init; } = false;
}
