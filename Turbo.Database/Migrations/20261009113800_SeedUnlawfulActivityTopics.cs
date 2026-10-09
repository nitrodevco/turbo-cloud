using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;

#nullable disable

namespace Turbo.Database.Migrations
{
    /// <summary>
    /// Adds the unlawful activity category the client's report flow has its own step for: the
    /// AS3 TopicsFlowHelpController asks a name and an email for a topic of the category named
    /// <c>unlawful_activity</c>. Its texts are help.cfh.reason.unlawful_activity, and the only
    /// topics whose help.cfh.topic.&lt;id&gt; texts fit it are 39 ("Sexual Abuse and
    /// Exploitation of Children (Anonymous Report)") and 40 ("Unlawful activity"). The topic
    /// names are ours (nothing shows Habbo's); the client looks a topic up by name only for the
    /// room report. Last in the list, after the categories AddCfhTopics seeded. INSERT IGNORE,
    /// so a hotel that changed them keeps what it chose.
    /// </summary>
    [DbContext(typeof(TurboDbContext))]
    [Migration("20261009113800_SeedUnlawfulActivityTopics")]
    public partial class SeedUnlawfulActivityTopics : Migration
    {
        private const string SEED =
            "INSERT IGNORE INTO cfh_topics (id, category, name, consequence, sort_order, enabled) VALUES "
            + "(39, 'unlawful_activity', 'child_sexual_abuse', 'mods', 700, 1), "
            + "(40, 'unlawful_activity', 'unlawful_activity', 'mods', 701, 1);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SEED);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A seeded row is indistinguishable from an operator's once edited; it stays.
        }
    }
}
