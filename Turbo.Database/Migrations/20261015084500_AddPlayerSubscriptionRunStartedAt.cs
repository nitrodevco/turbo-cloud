using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// When the membership running now started (<c>player_subscriptions.run_started_at</c>), for
    /// the club centre's current streak. Existing rows stay null and keep counting every day used up.
    /// </summary>
    public partial class AddPlayerSubscriptionRunStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "run_started_at",
                table: "player_subscriptions",
                type: "datetime(6)",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "run_started_at", table: "player_subscriptions");
        }
    }
}
