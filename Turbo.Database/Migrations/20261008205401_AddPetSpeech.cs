using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Adds <c>pet_speech</c>, what each pet type says when told to speak or on its own, and
    /// seeds it per type. It replaces the one <c>Turbo:Pets:SpeechLines</c> list every type
    /// shared, which had cats barking.
    /// </summary>
    public partial class AddPetSpeech : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .CreateTable(
                    name: "pet_speech",
                    columns: table => new
                    {
                        id = table
                            .Column<int>(type: "int", nullable: false)
                            .Annotation(
                                "MySql:ValueGenerationStrategy",
                                MySqlValueGenerationStrategy.IdentityColumn
                            ),
                        type_id = table.Column<int>(type: "int", nullable: true),
                        line = table
                            .Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
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
                        table.PrimaryKey("PK_pet_speech", x => x.id);
                    }
                )
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_pet_speech_type_id",
                table: "pet_speech",
                column: "type_id"
            );

            // A cat says what a cat says. Types follow the client's pet.configuration order;
            // the monsterplant (16) has nothing to say. Edit these rows to change what pets say.
            migrationBuilder.InsertData(
                table: "pet_speech",
                columns: ["type_id", "line"],
                values: new object[,]
                {
                    { 0, "Woof!" },
                    { 0, "Arf!" },
                    { 0, "Ruff ruff!" },
                    { 0, "*wags tail*" },
                    { 1, "Meow!" },
                    { 1, "Purrr..." },
                    { 1, "Mew?" },
                    { 1, "*stretches*" },
                    { 2, "Snap!" },
                    { 2, "Hsss..." },
                    { 2, "*grins*" },
                    { 3, "Yap!" },
                    { 3, "Arf arf!" },
                    { 3, "Grrr..." },
                    { 4, "Grrr!" },
                    { 4, "Rawr!" },
                    { 4, "*sniffs the air*" },
                    { 5, "Oink!" },
                    { 5, "Oink oink!" },
                    { 5, "*snorts*" },
                    { 6, "Roar!" },
                    { 6, "Rrrawr!" },
                    { 6, "*yawns*" },
                    { 7, "Hrumph!" },
                    { 7, "*snorts*" },
                    { 7, "*stomps*" },
                    { 8, "Hsss..." },
                    { 8, "*clicks*" },
                    { 8, "*spins a web*" },
                    { 9, "..." },
                    { 9, "*yawns*" },
                    { 9, "*peeks out of its shell*" },
                    { 10, "Cluck!" },
                    { 10, "Bawk bawk!" },
                    { 10, "Cheep!" },
                    { 11, "Ribbit!" },
                    { 11, "Croak!" },
                    { 11, "*blinks*" },
                    { 12, "Rawr!" },
                    { 12, "Grrrr!" },
                    { 12, "*puffs smoke*" },
                    { 13, "Grrr!" },
                    { 13, "Rawr!" },
                    { 14, "Ooh ooh aah aah!" },
                    { 14, "Eek!" },
                    { 14, "*scratches head*" },
                    { 15, "Neigh!" },
                    { 15, "*snorts*" },
                    { 15, "*stomps*" },
                    { 17, "Squeak!" },
                    { 17, "*twitches nose*" },
                    { 17, "*hops*" },
                    { 18, "Hehehe..." },
                    { 18, "*glares*" },
                    { 18, "*twitches nose*" },
                    { 19, "Sigh..." },
                    { 19, "..." },
                    { 19, "*sniffs*" },
                    { 20, "<3" },
                    { 20, "*blows a kiss*" },
                    { 20, "*hops*" },
                    { 21, "Coo!" },
                    { 21, "Coo coo!" },
                    { 22, "Coo..." },
                    { 22, "*glares*" },
                    { 23, "Hehehe..." },
                    { 23, "Eek!" },
                    { 24, "Grr!" },
                    { 24, "*yawns*" },
                    { 25, "Yip!" },
                    { 25, "Arf!" },
                    { 26, "Hello there!" },
                    { 26, "Hmph!" },
                    { 26, "*tips hat*" },
                    { 27, "Top o' the mornin'!" },
                    { 27, "*jingles coins*" },
                    { 28, "Mew!" },
                    { 28, "Mew mew!" },
                    { 28, "*purrs*" },
                    { 29, "Yip!" },
                    { 29, "Arf!" },
                    { 29, "*wags tail*" },
                    { 30, "Oink!" },
                    { 30, "*squeals*" },
                    { 31, "Haloo!" },
                    { 31, "*hums*" },
                    { 32, "Hehe!" },
                    { 32, "*juggles*" },
                    { 33, "Screech!" },
                    { 33, "Skraa!" },
                    { 34, "Skreee!" },
                    { 34, "Rawr!" },
                    { 34, "*clicks claws*" },
                    { 35, "Moo!" },
                    { 35, "Mooo!" },
                    { 35, "*chews*" },
                    { 36, "Woof!" },
                    { 36, "Rawr!" },
                    { 36, "*puffs smoke*" },
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "pet_speech");
        }
    }
}
