using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagExplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseFolderStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TagAssignments_AutoRuleParentTagId",
                table: "TagAssignments");

            migrationBuilder.DropColumn(
                name: "AutoRuleParentTagId",
                table: "TagAssignments");

            migrationBuilder.AddColumn<int>(
                name: "DirectoryCount",
                table: "Folders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FileCount",
                table: "Folders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxDepth",
                table: "Folders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatsTimestampUtc",
                table: "Folders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DirectoryCount",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "FileCount",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "MaxDepth",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "StatsTimestampUtc",
                table: "Folders");

            migrationBuilder.AddColumn<int>(
                name: "AutoRuleParentTagId",
                table: "TagAssignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagAssignments_AutoRuleParentTagId",
                table: "TagAssignments",
                column: "AutoRuleParentTagId");
        }
    }
}
