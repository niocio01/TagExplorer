using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TagExplorer.Migrations
{
    /// <inheritdoc />
    public partial class RenamedFolderBaseTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AutoAssignmentRules_AutoAssignmentRules_FolderId",
                table: "AutoAssignmentRules");

            migrationBuilder.DropForeignKey(
                name: "FK_AutoAssignmentRules_AutoAssignmentRules_ParentFolderId",
                table: "AutoAssignmentRules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AutoAssignmentRules",
                table: "AutoAssignmentRules");

            migrationBuilder.RenameTable(
                name: "AutoAssignmentRules",
                newName: "Folders");

            migrationBuilder.RenameIndex(
                name: "IX_AutoAssignmentRules_ParentFolderId",
                table: "Folders",
                newName: "IX_Folders_ParentFolderId");

            migrationBuilder.RenameIndex(
                name: "IX_AutoAssignmentRules_FolderId",
                table: "Folders",
                newName: "IX_Folders_FolderId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Folders",
                table: "Folders",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Folders_FolderId",
                table: "Folders",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Folders_ParentFolderId",
                table: "Folders",
                column: "ParentFolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Folders_FolderId",
                table: "Folders");

            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Folders_ParentFolderId",
                table: "Folders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Folders",
                table: "Folders");

            migrationBuilder.RenameTable(
                name: "Folders",
                newName: "AutoAssignmentRules");

            migrationBuilder.RenameIndex(
                name: "IX_Folders_ParentFolderId",
                table: "AutoAssignmentRules",
                newName: "IX_AutoAssignmentRules_ParentFolderId");

            migrationBuilder.RenameIndex(
                name: "IX_Folders_FolderId",
                table: "AutoAssignmentRules",
                newName: "IX_AutoAssignmentRules_FolderId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AutoAssignmentRules",
                table: "AutoAssignmentRules",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AutoAssignmentRules_AutoAssignmentRules_FolderId",
                table: "AutoAssignmentRules",
                column: "FolderId",
                principalTable: "AutoAssignmentRules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AutoAssignmentRules_AutoAssignmentRules_ParentFolderId",
                table: "AutoAssignmentRules",
                column: "ParentFolderId",
                principalTable: "AutoAssignmentRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
