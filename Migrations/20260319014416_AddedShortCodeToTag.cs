using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagExplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddedShortCodeToTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortCode",
                table: "Tags",
                type: "varchar(8)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortCode",
                table: "Tags");
        }
    }
}
