using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TagExplorer.Migrations
{
    /// <inheritdoc />
    public partial class AddedFolderTableClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaseFolders");

            migrationBuilder.CreateTable(
                name: "AutoAssignmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    FolderType = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    ParentFolderId = table.Column<int>(type: "integer", nullable: true),
                    FolderId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoAssignmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutoAssignmentRules_AutoAssignmentRules_FolderId",
                        column: x => x.FolderId,
                        principalTable: "AutoAssignmentRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AutoAssignmentRules_AutoAssignmentRules_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "AutoAssignmentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoAssignmentRules_FolderId",
                table: "AutoAssignmentRules",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoAssignmentRules_ParentFolderId",
                table: "AutoAssignmentRules",
                column: "ParentFolderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoAssignmentRules");

            migrationBuilder.CreateTable(
                name: "BaseFolders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Path = table.Column<string>(type: "text", nullable: true),
                    Selected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseFolders", x => x.Id);
                });
        }
    }
}
