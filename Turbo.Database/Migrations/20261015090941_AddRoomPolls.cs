using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "room_polls",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        room_id = table.Column<int>(type: "int", nullable: false),
                        type = table
                            .Column<string>(
                                type: "varchar(512)",
                                maxLength: 512,
                                nullable: false,
                                defaultValue: ""
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        headline = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        summary = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        start_message = table
                            .Column<string>(
                                type: "varchar(512)",
                                maxLength: 512,
                                nullable: false,
                                defaultValue: ""
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        end_message = table
                            .Column<string>(
                                type: "varchar(512)",
                                maxLength: 512,
                                nullable: false,
                                defaultValue: ""
                            )
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        nps_poll = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: false
                        ),
                        enabled = table.Column<bool>(
                            type: "tinyint(1)",
                            nullable: false,
                            defaultValue: true
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
                        table.PrimaryKey("PK_room_polls", x => x.id);
                        table.ForeignKey(
                            name: "FK_room_polls_rooms_room_id",
                            column: x => x.room_id,
                            principalTable: "rooms",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_room_polls",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        poll_id = table.Column<int>(type: "int", nullable: false),
                        state = table.Column<int>(type: "int", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_room_polls", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_room_polls_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_player_room_polls_room_polls_poll_id",
                            column: x => x.poll_id,
                            principalTable: "room_polls",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "room_poll_questions",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        poll_id = table.Column<int>(type: "int", nullable: false),
                        parent_question_id = table.Column<int>(type: "int", nullable: true),
                        sort_order = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        question_type = table.Column<int>(type: "int", nullable: false),
                        text = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        category = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                        answer_type = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
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
                        table.PrimaryKey("PK_room_poll_questions", x => x.id);
                        table.ForeignKey(
                            name: "FK_room_poll_questions_room_polls_poll_id",
                            column: x => x.poll_id,
                            principalTable: "room_polls",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "room_poll_answers",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        poll_id = table.Column<int>(type: "int", nullable: false),
                        question_id = table.Column<int>(type: "int", nullable: false),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        answer = table
                            .Column<string>(type: "text", maxLength: 512, nullable: false)
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
                        table.PrimaryKey("PK_room_poll_answers", x => x.id);
                        table.ForeignKey(
                            name: "FK_room_poll_answers_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_room_poll_answers_room_poll_questions_question_id",
                            column: x => x.question_id,
                            principalTable: "room_poll_questions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_room_poll_answers_room_polls_poll_id",
                            column: x => x.poll_id,
                            principalTable: "room_polls",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "room_poll_choices",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        question_id = table.Column<int>(type: "int", nullable: false),
                        sort_order = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
                        ),
                        value = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        text = table
                            .Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        choice_type = table.Column<int>(
                            type: "int",
                            nullable: false,
                            defaultValue: 0
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
                        table.PrimaryKey("PK_room_poll_choices", x => x.id);
                        table.ForeignKey(
                            name: "FK_room_poll_choices_room_poll_questions_question_id",
                            column: x => x.question_id,
                            principalTable: "room_poll_questions",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_player_room_polls_player_id_poll_id",
                table: "player_room_polls",
                columns: new[] { "player_id", "poll_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_room_polls_poll_id",
                table: "player_room_polls",
                column: "poll_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_poll_answers_player_id_question_id",
                table: "room_poll_answers",
                columns: new[] { "player_id", "question_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_poll_answers_poll_id",
                table: "room_poll_answers",
                column: "poll_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_poll_answers_question_id",
                table: "room_poll_answers",
                column: "question_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_poll_choices_question_id_sort_order",
                table: "room_poll_choices",
                columns: new[] { "question_id", "sort_order" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_poll_questions_poll_id_sort_order",
                table: "room_poll_questions",
                columns: new[] { "poll_id", "sort_order" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_room_polls_room_id_enabled",
                table: "room_polls",
                columns: new[] { "room_id", "enabled" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "player_room_polls");

            migrationBuilder.DropTable(name: "room_poll_answers");

            migrationBuilder.DropTable(name: "room_poll_choices");

            migrationBuilder.DropTable(name: "room_poll_questions");

            migrationBuilder.DropTable(name: "room_polls");
        }
    }
}
