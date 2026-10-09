using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildForums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "guild_forum_read_markers",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        guild_id = table.Column<int>(type: "int", nullable: false),
                        last_read_message_id = table.Column<int>(type: "int", nullable: false),
                        read_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_guild_forum_read_markers", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "guild_forum_threads",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        guild_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        subject = table
                            .Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        is_sticky = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        is_locked = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        state = table.Column<byte>(
                            type: "tinyint unsigned",
                            nullable: false,
                            defaultValue: (byte)0
                        ),
                        moderator_id = table.Column<int>(type: "int", nullable: true),
                        moderated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        message_count = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        last_message_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: false
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_guild_forum_threads", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "guild_forums",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        guild_id = table.Column<int>(type: "int", nullable: false),
                        read_permission = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        post_message_permission = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 1
                        ),
                        post_thread_permission = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 1
                        ),
                        moderate_permission = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 2
                        ),
                        thread_count = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        message_count = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        last_message_player_id = table.Column<int>(type: "int", nullable: true),
                        last_message_at = table.Column<DateTime>(
                            type: "datetime(6)",
                            nullable: true
                        ),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_guild_forums", x => x.id);
                        table.ForeignKey(
                            name: "FK_guild_forums_guilds_guild_id",
                            column: x => x.guild_id,
                            principalTable: "guilds",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "guild_forum_messages",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        guild_id = table.Column<int>(type: "int", nullable: false),
                        thread_id = table.Column<int>(type: "int", nullable: false),
                        forum_message_id = table.Column<int>(type: "int", nullable: false),
                        thread_index = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        text = table
                            .Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        state = table.Column<byte>(
                            type: "tinyint unsigned",
                            nullable: false,
                            defaultValue: (byte)0
                        ),
                        moderator_id = table.Column<int>(type: "int", nullable: true),
                        moderated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_guild_forum_messages", x => x.id);
                        table.ForeignKey(
                            name: "FK_guild_forum_messages_guild_forum_threads_thread_id",
                            column: x => x.thread_id,
                            principalTable: "guild_forum_threads",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_guild_forum_messages_guild_id_forum_message_id",
                table: "guild_forum_messages",
                columns: new[] { "guild_id", "forum_message_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_guild_forum_messages_thread_id_thread_index",
                table: "guild_forum_messages",
                columns: new[] { "thread_id", "thread_index" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_guild_forum_read_markers_guild_id_read_at",
                table: "guild_forum_read_markers",
                columns: new[] { "guild_id", "read_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_guild_forum_read_markers_player_id_guild_id",
                table: "guild_forum_read_markers",
                columns: new[] { "player_id", "guild_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_guild_forum_threads_guild_id_is_sticky_last_message_at",
                table: "guild_forum_threads",
                columns: new[] { "guild_id", "is_sticky", "last_message_at" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_guild_forums_guild_id",
                table: "guild_forums",
                column: "guild_id",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "guild_forum_messages");

            migrationBuilder.DropTable(name: "guild_forum_read_markers");

            migrationBuilder.DropTable(name: "guild_forums");

            migrationBuilder.DropTable(name: "guild_forum_threads");
        }
    }
}
