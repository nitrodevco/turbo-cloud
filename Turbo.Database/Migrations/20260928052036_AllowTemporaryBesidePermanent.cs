using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Lets a permanent and a temporary assignment of the same node, meta key or group sit side by
    /// side, so a timed sanction no longer overwrites the permanent value it suspends: each
    /// permission table gains <c>is_temporary</c>, backfilled from <c>expires_at</c>, and it joins
    /// the unique key.
    /// <para>
    /// The new keys go in before the old ones come out. MySQL refuses to drop an index a foreign
    /// key on <c>player_id</c> or <c>group_id</c> stands on until another index begins with that
    /// column, which the new key does.
    /// </para>
    /// </summary>
    public partial class AllowTemporaryBesidePermanent : Migration
    {
        private static readonly (string Table, string Owner, string Key)[] TABLES =
        [
            ("player_permission_nodes", "player_id", "node"),
            ("player_permission_meta", "player_id", "meta_key"),
            ("player_permission_groups", "player_id", "group_id"),
            ("permission_group_nodes", "group_id", "node"),
            ("permission_group_meta", "group_id", "meta_key"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, owner, key) in TABLES)
            {
                migrationBuilder.AddColumn<bool>(
                    name: "is_temporary",
                    table: table,
                    type: "tinyint(1)",
                    nullable: false,
                    defaultValue: false
                );

                migrationBuilder.Sql($"UPDATE {table} SET is_temporary = expires_at IS NOT NULL;");

                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_{owner}_{key}_is_temporary",
                    table: table,
                    columns: [owner, key, "is_temporary"],
                    unique: true
                );

                migrationBuilder.DropIndex(name: $"IX_{table}_{owner}_{key}", table: table);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, owner, key) in TABLES)
            {
                // The old key allows one row per node; where a temporary row sits beside a
                // permanent one, the temporary one goes.
                migrationBuilder.Sql(
                    $"DELETE t FROM {table} t JOIN {table} p ON p.{owner} = t.{owner} AND p.{key} = t.{key} AND p.is_temporary = 0 WHERE t.is_temporary = 1;"
                );

                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_{owner}_{key}",
                    table: table,
                    columns: [owner, key],
                    unique: true
                );

                migrationBuilder.DropIndex(
                    name: $"IX_{table}_{owner}_{key}_is_temporary",
                    table: table
                );

                migrationBuilder.DropColumn(name: "is_temporary", table: table);
            }
        }
    }
}
