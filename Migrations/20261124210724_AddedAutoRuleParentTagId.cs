using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagExplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddedAutoRuleParentTagId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutoRuleParentTagId",
                table: "TagAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagAssignments_AutoRuleParentTagId",
                table: "TagAssignments",
                column: "AutoRuleParentTagId");

            // The model snapshot still holds these constraints, but PostgreSQL dropped them together with the
            // column in AddBaseFolderStats, so EF doesn't generate them. Recreate them by hand.
            migrationBuilder.Sql(
                "ALTER TABLE \"TagAssignments\" ADD CONSTRAINT \"CK_TagAssignments_Manual_AutoRuleParentTagIdNull\" " +
                "CHECK (\"Kind\" <> 0 OR (\"TagId\" IS NOT NULL AND \"AutoRuleParentTagId\" IS NULL));");
            migrationBuilder.Sql(
                "ALTER TABLE \"TagAssignments\" ADD CONSTRAINT \"CK_TagAssignments_AutoDirectChildren_AutoRuleParentTagIdRequired\" " +
                "CHECK (\"Kind\" <> 10 OR (\"TargetType\" = 0 AND \"TagId\" IS NULL AND \"AutoRuleParentTagId\" IS NOT NULL));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"TagAssignments\" DROP CONSTRAINT IF EXISTS \"CK_TagAssignments_AutoDirectChildren_AutoRuleParentTagIdRequired\";");
            migrationBuilder.Sql("ALTER TABLE \"TagAssignments\" DROP CONSTRAINT IF EXISTS \"CK_TagAssignments_Manual_AutoRuleParentTagIdNull\";");

            migrationBuilder.DropIndex(
                name: "IX_TagAssignments_AutoRuleParentTagId",
                table: "TagAssignments");

            migrationBuilder.DropColumn(
                name: "AutoRuleParentTagId",
                table: "TagAssignments");
        }
    }
}
