using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Furniture;

namespace Turbo.Rooms.Configuration;

/// <summary>The recolorable palettes as <see cref="RoomConfig"/> sets them, read as they are now.</summary>
public sealed class RecolorablePalettes(IOptionsMonitor<RoomConfig> config) : IRecolorablePalettes
{
    public IReadOnlyList<int> PaletteOf(string definitionName) =>
        RecolorableData.GetPalette(
            config.CurrentValue.RecolorablePaletteToFurni,
            config.CurrentValue.RecolorablePalettes,
            RecolorableData.ClassName(definitionName)
        );
}
