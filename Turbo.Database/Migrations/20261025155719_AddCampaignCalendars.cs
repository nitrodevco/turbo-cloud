using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "campaign_calendars",
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
                        image = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        starts_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                        days = table.Column<int>(type: "int", nullable: false),
                        enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_campaign_calendars", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "campaign_calendar_rewards",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        campaign_id = table.Column<int>(type: "int", nullable: false),
                        day_index = table.Column<int>(type: "int", nullable: false),
                        product_code = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        custom_image = table
                            .Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        furniture_class_name = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        badge_code = table
                            .Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        credits = table.Column<int>(type: "int", nullable: false),
                        currency_type_id = table.Column<int>(type: "int", nullable: true),
                        currency_amount = table.Column<int>(type: "int", nullable: false),
                        effect_id = table.Column<int>(type: "int", nullable: true),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_campaign_calendar_rewards", x => x.id);
                        table.ForeignKey(
                            name: "FK_campaign_calendar_rewards_campaign_calendars_campaign_id",
                            column: x => x.campaign_id,
                            principalTable: "campaign_calendars",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "player_campaign_calendar_doors",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        player_id = table.Column<int>(type: "int", nullable: false),
                        campaign_id = table.Column<int>(type: "int", nullable: false),
                        day_index = table.Column<int>(type: "int", nullable: false),
                        by_staff = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        created_at = table
                            .Column<DateTime>(type: "datetime(6)", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_player_campaign_calendar_doors", x => x.id);
                        table.ForeignKey(
                            name: "FK_player_campaign_calendar_doors_campaign_calendars_campaign_id",
                            column: x => x.campaign_id,
                            principalTable: "campaign_calendars",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                        table.ForeignKey(
                            name: "FK_player_campaign_calendar_doors_players_player_id",
                            column: x => x.player_id,
                            principalTable: "players",
                            principalColumn: "id",
                            onDelete: ReferentialAction.Cascade
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_calendar_rewards_campaign_id_day_index",
                table: "campaign_calendar_rewards",
                columns: new[] { "campaign_id", "day_index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_campaign_calendars_name",
                table: "campaign_calendars",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_campaign_calendar_doors_campaign_id",
                table: "player_campaign_calendar_doors",
                column: "campaign_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_player_campaign_calendar_doors_player_id_campaign_id_day_ind~",
                table: "player_campaign_calendar_doors",
                columns: new[] { "player_id", "campaign_id", "day_index" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "campaign_calendar_rewards");

            migrationBuilder.DropTable(name: "player_campaign_calendar_doors");

            migrationBuilder.DropTable(name: "campaign_calendars");
        }
    }
}
