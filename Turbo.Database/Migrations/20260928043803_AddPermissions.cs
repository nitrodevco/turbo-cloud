using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// The permission tables (<c>docs/permissions.md</c>), and <c>navigator_flatcats.required_node</c>
    /// (§16). A permanent and a temporary assignment of the same node, meta key or group are
    /// separate rows, so <c>is_temporary</c> is part of each unique key. It defaults to false in the
    /// table (not in the model), so a CMS or hand-written insert that leaves it out adds a
    /// permanent row.
    /// <para>
    /// <c>players.perk_flags</c> leaves the model but stays in the table, although EF would drop
    /// it. Perks are projected from permissions now (§8), and <c>SeedPermissions</c> carries every
    /// flag that meant anything into player nodes, so nothing reads the column. It stays because
    /// CMS and housekeeping panels write the players table directly, and one that still inserts
    /// <c>perk_flags</c> would fail against a table without it; its default keeps rows written
    /// without it valid. Drop it in a later migration once nothing writes it.
    /// </para>
    /// </summary>
    public partial class AddPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AddColumn<string>(
                    name: "required_node",
                    table: "navigator_flatcats",
                    type: "varchar(128)",
                    maxLength: 128,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "permission_audit",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        actor_player_id = table.Column<int>(type: "int", nullable: true),
                        target_type = table.Column<int>(type: "int", nullable: false),
                        target_id = table.Column<int>(type: "int", nullable: false),
                        action = table.Column<int>(type: "int", nullable: false),
                        subject = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        value = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_permission_audit", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "permission_groups",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        name = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        display_name = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        weight = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_permission_groups", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_permission_meta",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        meta_key = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        value = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        is_temporary = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_permission_meta", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_permission_meta_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_permission_nodes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        node = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        value = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        is_temporary = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_permission_nodes", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_permission_nodes_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "permission_group_meta",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        group_id = table.Column<int>(type: "int", nullable: false),
                        meta_key = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        value = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        is_temporary = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_permission_group_meta", x => x.id);
                        table.ForeignKey(
                            name: "FK_permission_group_meta_permission_groups_group_id",
                            column: x => x.group_id,
                            principalTable: "permission_groups",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "permission_group_nodes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        group_id = table.Column<int>(type: "int", nullable: false),
                        node = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        value = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        is_temporary = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_permission_group_nodes", x => x.id);
                        table.ForeignKey(
                            name: "FK_permission_group_nodes_permission_groups_group_id",
                            column: x => x.group_id,
                            principalTable: "permission_groups",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "permission_group_parents",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        group_id = table.Column<int>(type: "int", nullable: false),
                        parent_group_id = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_permission_group_parents", x => x.id);
                        table.ForeignKey(
                            name: "FK_permission_group_parents_permission_groups_group_id",
                            column: x => x.group_id,
                            principalTable: "permission_groups",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_permission_group_parents_permission_groups_parent_group_id",
                            column: x => x.parent_group_id,
                            principalTable: "permission_groups",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_permission_groups",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        group_id = table.Column<int>(type: "int", nullable: false),
                        expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        is_temporary = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        updated_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                        deleted_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: true)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.ComputedColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_permission_groups", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_permission_groups_permission_groups_group_id",
                            column: x => x.group_id,
                            principalTable: "permission_groups",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_player_permission_groups_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_permission_audit_target_type_target_id_created_at",
                table: "permission_audit",
                columns: new[] { "target_type", "target_id", "created_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_permission_group_meta_group_id_meta_key_is_temporary",
                table: "permission_group_meta",
                columns: new[] { "group_id", "meta_key", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_permission_group_nodes_group_id_node_is_temporary",
                table: "permission_group_nodes",
                columns: new[] { "group_id", "node", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_permission_group_parents_group_id_parent_group_id",
                table: "permission_group_parents",
                columns: new[] { "group_id", "parent_group_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_permission_group_parents_parent_group_id",
                table: "permission_group_parents",
                column: "parent_group_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_permission_groups_name",
                table: "permission_groups",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_permission_groups_group_id",
                table: "player_permission_groups",
                column: "group_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_permission_groups_player_id_group_id_is_temporary",
                table: "player_permission_groups",
                columns: new[] { "player_id", "group_id", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_permission_meta_player_id_meta_key_is_temporary",
                table: "player_permission_meta",
                columns: new[] { "player_id", "meta_key", "is_temporary" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_permission_nodes_player_id_node_is_temporary",
                table: "player_permission_nodes",
                columns: new[] { "player_id", "node", "is_temporary" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "permission_audit");

            migrationBuilder.DropTable(name: "permission_group_meta");

            migrationBuilder.DropTable(name: "permission_group_nodes");

            migrationBuilder.DropTable(name: "permission_group_parents");

            migrationBuilder.DropTable(name: "player_permission_groups");

            migrationBuilder.DropTable(name: "player_permission_meta");

            migrationBuilder.DropTable(name: "player_permission_nodes");

            migrationBuilder.DropTable(name: "permission_groups");

            migrationBuilder.DropColumn(name: "required_node", table: "navigator_flatcats");
        }
    }
}
