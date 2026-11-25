using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagExplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddedAutoSourcePathToTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoSourcePath",
                table: "Tags",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true);

            // Child tags stored by the first version of child-tag rules ("Auto: <subfolder path>" description) are
            // now generated at runtime. Link them to their subfolder, then delete the ones nobody changed or uses.
            migrationBuilder.Sql(
                "UPDATE \"Tags\" SET \"AutoSourcePath\" = substring(\"Description\" from 7) " +
                "WHERE \"ParentId\" IS NOT NULL AND \"Description\" LIKE 'Auto: %';");
            migrationBuilder.Sql(
                "DELETE FROM \"Tags\" t USING \"Tags\" p " +
                "WHERE t.\"ParentId\" = p.\"Id\" AND t.\"AutoSourcePath\" IS NOT NULL " +
                "AND lower(t.\"Name\") = lower(left(reverse(split_part(reverse(t.\"AutoSourcePath\"), chr(92), 1)), 50)) " +
                "AND t.\"Aliases\" IS NULL AND t.\"ShortCode\" IS NULL " +
                "AND t.\"ColorId\" = p.\"ColorId\" AND t.\"IconName\" = p.\"IconName\" " +
                "AND NOT EXISTS (SELECT 1 FROM \"TagAssignments\" a WHERE (a.\"TagId\" = t.\"Id\" OR a.\"AutoRuleParentTagId\" = t.\"Id\") AND NOT a.\"IsArchived\") " +
                "AND NOT EXISTS (SELECT 1 FROM \"Tags\" c WHERE c.\"ParentId\" = t.\"Id\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoSourcePath",
                table: "Tags");
        }
    }
}
