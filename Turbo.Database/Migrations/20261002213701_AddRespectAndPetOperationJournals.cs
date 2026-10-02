using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRespectAndPetOperationJournals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "human_respect_operations",
                    columns: table => new
                    {
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        ActorId = table.Column<int>(type: "int", nullable: false),
                        TargetId = table.Column<int>(type: "int", nullable: false),
                        Rejected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        ResultTotal = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_human_respect_operations", x => x.OperationId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "human_respect_participant_receipts",
                    columns: table => new
                    {
                        PlayerId = table.Column<int>(type: "int", nullable: false),
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Kind = table
                            .Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        Accepted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        ResultTotal = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey(
                            "PK_human_respect_participant_receipts",
                            x => new
                            {
                                x.PlayerId,
                                x.OperationId,
                                x.Kind,
                            }
                        );
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "pet_nutrition_operations",
                    columns: table => new
                    {
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        RoomId = table.Column<int>(type: "int", nullable: false),
                        PetId = table.Column<int>(type: "int", nullable: false),
                        OwnerId = table.Column<int>(type: "int", nullable: false),
                        SupplierId = table.Column<int>(type: "int", nullable: false),
                        BaseNutrition = table.Column<int>(type: "int", nullable: false),
                        RequestedNutrition = table.Column<int>(type: "int", nullable: false),
                        MaxNutrition = table.Column<int>(type: "int", nullable: false),
                        NutritionAfter = table.Column<int>(type: "int", nullable: false),
                        ActualGain = table.Column<int>(type: "int", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_pet_nutrition_operations", x => x.OperationId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder
                .CreateTable(
                    name: "pet_respect_operations",
                    columns: table => new
                    {
                        OperationId = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                            .Annotation("MySql:CharSet", "utf8mb4"),
                        RoomId = table.Column<int>(type: "int", nullable: false),
                        ActorId = table.Column<int>(type: "int", nullable: false),
                        PetId = table.Column<int>(type: "int", nullable: false),
                        OwnerId = table.Column<int>(type: "int", nullable: false),
                        BaseRespect = table.Column<int>(type: "int", nullable: false),
                        Rejected = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        Completed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                        ResultRespect = table.Column<int>(type: "int", nullable: false),
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_pet_respect_operations", x => x.OperationId);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "human_respect_operations");

            migrationBuilder.DropTable(name: "human_respect_participant_receipts");

            migrationBuilder.DropTable(name: "pet_nutrition_operations");

            migrationBuilder.DropTable(name: "pet_respect_operations");
        }
    }
}
