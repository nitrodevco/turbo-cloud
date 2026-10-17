using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Takes away the badges named <c>false</c> and <c>true</c>, which no hotel badge is: a
    /// <c>:gift</c> whose last word was a yes/no flag gave it as a badge code until 2026-10-13, so
    /// players were left with an empty badge (<c>badge_name_false</c>) in their badges and on
    /// their profile. A definition under either code goes with them. Nothing else is a badge by
    /// those names, so running it again changes nothing. Data only: the model is unchanged, so
    /// there is no designer snapshot. The rows are gone for good, so it cannot be taken back.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261017234000_RemoveFalseBadges")]
    public partial class RemoveFalseBadges : Migration
    {
        private const string CODES = "('false', 'true')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"DELETE FROM `player_badges` WHERE `badge_code` IN {CODES};");
            migrationBuilder.Sql($"DELETE FROM `badge_definitions` WHERE `badge_code` IN {CODES};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
