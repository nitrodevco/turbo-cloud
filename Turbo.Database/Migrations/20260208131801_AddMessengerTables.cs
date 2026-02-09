using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "messenger_blocked",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        blocked_player_id = table.Column<int>(type: "int", nullable: false),
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
                        table.PrimaryKey("PK_messenger_blocked", x => x.id);
                        table.ForeignKey(
                            name: "FK_messenger_blocked_players_blocked_player_id",
                            column: x => x.blocked_player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_messenger_blocked_players_player_id",
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
                    name: "messenger_ignored",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        ignored_player_id = table.Column<int>(type: "int", nullable: false),
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
                        table.PrimaryKey("PK_messenger_ignored", x => x.id);
                        table.ForeignKey(
                            name: "FK_messenger_ignored_players_ignored_player_id",
                            column: x => x.ignored_player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_messenger_ignored_players_player_id",
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
                    name: "messenger_messages",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        sender_id = table.Column<int>(type: "int", nullable: false),
                        receiver_id = table.Column<int>(type: "int", nullable: false),
                        message = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        timestamp = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                        table.PrimaryKey("PK_messenger_messages", x => x.id);
                        table.ForeignKey(
                            name: "FK_messenger_messages_players_receiver_id",
                            column: x => x.receiver_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_messenger_messages_players_sender_id",
                            column: x => x.sender_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_messenger_blocked_blocked_player_id",
                table: "messenger_blocked",
                column: "blocked_player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_messenger_blocked_player_id_blocked_player_id",
                table: "messenger_blocked",
                columns: new[] { "player_id", "blocked_player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_messenger_ignored_ignored_player_id",
                table: "messenger_ignored",
                column: "ignored_player_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_messenger_ignored_player_id_ignored_player_id",
                table: "messenger_ignored",
                columns: new[] { "player_id", "ignored_player_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_messenger_messages_receiver_id_sender_id_timestamp",
                table: "messenger_messages",
                columns: new[] { "receiver_id", "sender_id", "timestamp" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_messenger_messages_sender_id_receiver_id_timestamp",
                table: "messenger_messages",
                columns: new[] { "sender_id", "receiver_id", "timestamp" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "messenger_blocked");

            migrationBuilder.DropTable(name: "messenger_ignored");

            migrationBuilder.DropTable(name: "messenger_messages");
        }
    }
}
