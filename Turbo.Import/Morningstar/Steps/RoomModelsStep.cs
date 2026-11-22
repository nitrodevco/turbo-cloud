using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Room;
using Turbo.Import.Morningstar.Formats;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The hotel's room models (<c>room_models</c>): a model the target has by name is used as it is,
/// so Turbo's own <c>model_a</c> stays Turbo's; one it lacks is added. A room's own floor plan
/// (<c>room_models_custom</c>) is copied with its room, as Turbo's floor plan editor names one:
/// <c>room_&lt;id&gt;</c>.
/// </summary>
internal sealed class RoomModelsStep : IImportStep
{
    public const string TABLE = "room_models";

    public string Name => "room models";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        foreach (
            var model in await ctx
                .Db.RoomModels.AsNoTracking()
                .Select(x => new { x.Id, x.Name })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            ctx.RoomModels[model.Name] = model.Id;

        await foreach (
            var row in ctx.Source.ReadAsync("room_models", "name", ct).ConfigureAwait(false)
        )
        {
            var name = row.Text("name").Trim();

            if (name.Length == 0 || ctx.RoomModels.ContainsKey(name))
                continue;

            var heightmap = Heightmap.Clean(row.String("heightmap"));

            if (heightmap is null)
            {
                ctx.Report.Note(
                    "room model left out: its heightmap has a tile Turbo cannot read",
                    name
                );

                continue;
            }

            ctx.RoomModels[name] = await ctx.AddSetupAsync(
                    new RoomModelEntity
                    {
                        Name = name,
                        Model = heightmap,
                        DoorX = row.Int("door_x"),
                        DoorY = row.Int("door_y"),
                        DoorRotation = Directions.Rotation(row.Int("door_dir", 2)),
                        Enabled = true,
                        Custom = false,
                    },
                    TABLE,
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
