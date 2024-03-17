using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class proc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcListEntry_Containers_ContainerId",
                table: "ProcListEntry");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProcListEntry",
                table: "ProcListEntry");

            migrationBuilder.RenameTable(
                name: "ProcListEntry",
                newName: "ProcListEntries");

            migrationBuilder.RenameIndex(
                name: "IX_ProcListEntry_ContainerId",
                table: "ProcListEntries",
                newName: "IX_ProcListEntries_ContainerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProcListEntries",
                table: "ProcListEntries",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcListEntries_Containers_ContainerId",
                table: "ProcListEntries",
                column: "ContainerId",
                principalTable: "Containers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcListEntries_Containers_ContainerId",
                table: "ProcListEntries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProcListEntries",
                table: "ProcListEntries");

            migrationBuilder.RenameTable(
                name: "ProcListEntries",
                newName: "ProcListEntry");

            migrationBuilder.RenameIndex(
                name: "IX_ProcListEntries_ContainerId",
                table: "ProcListEntry",
                newName: "IX_ProcListEntry_ContainerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProcListEntry",
                table: "ProcListEntry",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcListEntry_Containers_ContainerId",
                table: "ProcListEntry",
                column: "ContainerId",
                principalTable: "Containers",
                principalColumn: "Id");
        }
    }
}
