using System.Linq;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// A fx box in the room with the config it was last built into. The signatures are how a
/// config or a status is compared with what a viewer was sent: a snapshot's extras are a
/// dictionary, which a record compares by reference.
/// </summary>
internal sealed record VariableFxBinding(
    FurnitureWiredVariableFxLogic Box,
    VariableFxConfigSnapshot Config
)
{
    public string Signature { get; } =
        string.Join(
            '|',
            Config.IsUserFx,
            Config.ShowMode,
            Config.UpdateMask,
            Config.ShowOnMouseHover,
            Config.ShowDurationMs,
            Config.Category,
            Config.StyleId,
            Config.ColorId,
            Config.WidthId,
            Config.RendererId,
            Config.DefaultMinValue,
            Config.DefaultMaxValue,
            string.Join(',', Config.Extra.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))
        );

    /// <summary>What a viewer sees of a status; "initialize" is how it is delivered, not what it says.</summary>
    public static string SignatureOf(VariableFxStatusSnapshot status) =>
        string.Join(
            '|',
            status.Value,
            status.OverrideMinValue,
            status.OverrideMaxValue,
            string.Join(',', status.Extra.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))
        );
}
