using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "asset_bundles",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        kind = table.Column<int>(type: "int", nullable: false),
                        name = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        revision = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        source = table.Column<int>(type: "int", nullable: false),
                        hash = table
                            .Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        size = table.Column<long>(type: "bigint", nullable: false),
                        ids = table
                            .Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        error = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        retry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_bundles", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "asset_publish_targets",
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
                        protocol = table.Column<int>(type: "int", nullable: false),
                        host = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        port = table.Column<int>(type: "int", nullable: false),
                        user = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        password = table.Column<byte[]>(type: "longblob", nullable: true),
                        remote_path = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        public_url = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        allow_self_signed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        host_key = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_publish_targets", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "asset_published_files",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        target_id = table.Column<int>(type: "int", nullable: false),
                        path = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        hash = table
                            .Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_published_files", x => x.id);
                        table.ForeignKey(
                            name: "FK_asset_published_files_asset_publish_targets_target_id",
                            column: x => x.target_id,
                            principalTable: "asset_publish_targets",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "asset_publishes",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        target_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        dry_run = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        uploaded = table.Column<int>(type: "int", nullable: false),
                        skipped = table.Column<int>(type: "int", nullable: false),
                        deleted = table.Column<int>(type: "int", nullable: false),
                        bytes = table.Column<long>(type: "bigint", nullable: false),
                        error = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        finished_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_asset_publishes", x => x.id);
                        table.ForeignKey(
                            name: "FK_asset_publishes_asset_publish_targets_target_id",
                            column: x => x.target_id,
                            principalTable: "asset_publish_targets",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_asset_bundles_kind_name",
                table: "asset_bundles",
                columns: new[] { "kind", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_asset_published_files_target_id_path",
                table: "asset_published_files",
                columns: new[] { "target_id", "path" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_asset_publishes_target_id",
                table: "asset_publishes",
                column: "target_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "asset_bundles");

            migrationBuilder.DropTable(name: "asset_published_files");

            migrationBuilder.DropTable(name: "asset_publishes");

            migrationBuilder.DropTable(name: "asset_publish_targets");
        }
    }
}
