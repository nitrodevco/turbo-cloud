using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddChatStyleLooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_html",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "notification",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder
                .AddColumn<string>(
                    name: "reg_points",
                    table: "player_chat_styles",
                    type: "longtext",
                    maxLength: 512,
                    nullable: true
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "player_chat_styles",
                type: "int",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<bool>(
                name: "staff_overrideable",
                table: "player_chat_styles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder
                .CreateTable(
                    name: "chat_style_library_bitmaps",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        name = table
                            .Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        data = table.Column<byte[]>(type: "longblob", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_chat_style_library_bitmaps", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_chat_style_bitmaps",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        chat_style_id = table.Column<int>(type: "int", nullable: false),
                        file = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        data = table.Column<byte[]>(type: "longblob", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_chat_style_bitmaps", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_chat_style_bitmaps_player_chat_styles_chat_style_id",
                            column: x => x.chat_style_id,
                            principalTable: "player_chat_styles",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_chat_style_library_bitmaps_name",
                table: "chat_style_library_bitmaps",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_chat_style_bitmaps_chat_style_id_file",
                table: "player_chat_style_bitmaps",
                columns: new[] { "chat_style_id", "file" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "chat_style_library_bitmaps");

            migrationBuilder.DropTable(name: "player_chat_style_bitmaps");

            migrationBuilder.DropColumn(name: "allow_html", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "notification", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "reg_points", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "sort_order", table: "player_chat_styles");

            migrationBuilder.DropColumn(name: "staff_overrideable", table: "player_chat_styles");
        }
    }
}
