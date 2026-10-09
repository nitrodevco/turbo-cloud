using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Widens <c>human_respect_participant_receipts.Kind</c> from 8 to 16 characters. A pet
    /// scratch records its spend as <c>pet-spend</c> (9), which MySQL refused, so every pet
    /// scratch failed and nothing was given.
    /// </summary>
    public partial class WidenRespectReceiptKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "Kind",
                    table: "human_respect_participant_receipts",
                    type: "varchar(16)",
                    maxLength: 16,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "varchar(8)",
                    oldMaxLength: 8
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterColumn<string>(
                    name: "Kind",
                    table: "human_respect_participant_receipts",
                    type: "varchar(8)",
                    maxLength: 8,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "varchar(16)",
                    oldMaxLength: 16
                )
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
