using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Gives Habbo's wired game furni the usage policy and state count Habbo has for them, where
    /// the hotel's data had room rights and a state too many for all of them. The usage is the
    /// one Habbo's own rooms send with every one of these items (an official client capture of
    /// 24 busy game rooms, build 1745: 239 Dark Tiles, 201 Arrow Plates, 196 tiles of the second
    /// kind, 86 blobs and the rest all agree per class): nobody uses an arrow plate, a tile, a
    /// ring or pressure plate or a blob by hand, wired does; anyone in the room flips a floor
    /// switch. The state count is the furni's own asset's (its visualization's states; the
    /// captured items only ever show those): a plate, a blob, an antenna, a gate, a glass door
    /// and a glow ball have two, so wired toggles them between the two instead of into a third
    /// that draws nothing. Only a definition still on the hotel's shipped values is changed, so
    /// running it again changes nothing and staff choices stand. Data only: the model is
    /// unchanged, so there is no designer snapshot.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261018020000_AlignWiredGameFurniUsage")]
    public partial class AlignWiredGameFurniUsage : Migration
    {
        /// <summary>
        /// Name, the usage policy (0 nobody, 1 room rights, 2 everyone) and state count Habbo
        /// has, and the ones the hotel shipped.
        /// </summary>
        private static readonly (
            string Name,
            int Usage,
            int States,
            int ShippedUsage,
            int ShippedStates
        )[] FURNI =
        [
            ("wf_arrowplate", 0, 2, 1, 3),
            ("wf_arrowplate_8dir", 0, 2, 1, 3),
            ("wf_ringplate", 0, 2, 1, 3),
            ("wf_pressureplate", 0, 2, 1, 3),
            ("wf_blob", 0, 2, 1, 3),
            ("wf_blob2", 0, 2, 1, 3),
            ("wf_button_arrow", 0, 2, 1, 2),
            ("wf_tile1", 0, 10, 1, 10),
            ("wf_tile2", 0, 10, 1, 10),
            ("wf_floor_switch1", 2, 2, 1, 2),
            ("wf_floor_switch2", 2, 4, 1, 4),
            ("wf_antenna1", 1, 2, 1, 3),
            ("wf_antenna2", 1, 2, 1, 3),
            ("wf_firegate", 1, 2, 1, 3),
            ("wf_glassdoor", 1, 2, 1, 3),
            ("wf_glowball", 1, 2, 1, 3),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, usage, states, shippedUsage, shippedStates) in FURNI)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `usage_policy` = {usage}, `total_states` = {states} WHERE `name` = '{name}' AND `usage_policy` = {shippedUsage} AND `total_states` = {shippedStates};"
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, usage, states, shippedUsage, shippedStates) in FURNI)
                migrationBuilder.Sql(
                    $"UPDATE `furniture_definitions` SET `usage_policy` = {shippedUsage}, `total_states` = {shippedStates} WHERE `name` = '{name}' AND `usage_policy` = {usage} AND `total_states` = {states};"
                );
        }
    }
}
