using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class DistributionInProcListEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WheightChance",
                table: "ProcListEntries",
                newName: "WeightChance");

            migrationBuilder.AddColumn<int>(
                name: "ProcListEntryId",
                table: "Distributions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_ProcListEntryId",
                table: "Distributions",
                column: "ProcListEntryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Distributions_ProcListEntries_ProcListEntryId",
                table: "Distributions",
                column: "ProcListEntryId",
                principalTable: "ProcListEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Distributions_ProcListEntries_ProcListEntryId",
                table: "Distributions");

            migrationBuilder.DropIndex(
                name: "IX_Distributions_ProcListEntryId",
                table: "Distributions");

            migrationBuilder.DropColumn(
                name: "ProcListEntryId",
                table: "Distributions");

            migrationBuilder.RenameColumn(
                name: "WeightChance",
                table: "ProcListEntries",
                newName: "WheightChance");
        }
    }
}
