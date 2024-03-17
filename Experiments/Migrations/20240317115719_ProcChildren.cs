using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ProcChildren : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Distributions_ProcListEntries_ProcListEntryId",
                table: "Distributions");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Containers_ContainerId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Distributions_DistributionId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Distributions_ProcListEntryId",
                table: "Distributions");

            migrationBuilder.AddColumn<int>(
                name: "ChildDistributionId",
                table: "ProcListEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChildDistributionId1",
                table: "ProcListEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistributionId",
                table: "ProcListEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcListEntries_ChildDistributionId1",
                table: "ProcListEntries",
                column: "ChildDistributionId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcListEntries_DistributionId",
                table: "ProcListEntries",
                column: "DistributionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Containers_ContainerId",
                table: "Items",
                column: "ContainerId",
                principalTable: "Containers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Distributions_DistributionId",
                table: "Items",
                column: "DistributionId",
                principalTable: "Distributions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcListEntries_Distributions_ChildDistributionId1",
                table: "ProcListEntries",
                column: "ChildDistributionId1",
                principalTable: "Distributions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcListEntries_Distributions_DistributionId",
                table: "ProcListEntries",
                column: "DistributionId",
                principalTable: "Distributions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Containers_ContainerId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Distributions_DistributionId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcListEntries_Distributions_ChildDistributionId1",
                table: "ProcListEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcListEntries_Distributions_DistributionId",
                table: "ProcListEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProcListEntries_ChildDistributionId1",
                table: "ProcListEntries");

            migrationBuilder.DropIndex(
                name: "IX_ProcListEntries_DistributionId",
                table: "ProcListEntries");

            migrationBuilder.DropColumn(
                name: "ChildDistributionId",
                table: "ProcListEntries");

            migrationBuilder.DropColumn(
                name: "ChildDistributionId1",
                table: "ProcListEntries");

            migrationBuilder.DropColumn(
                name: "DistributionId",
                table: "ProcListEntries");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Containers_ContainerId",
                table: "Items",
                column: "ContainerId",
                principalTable: "Containers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Distributions_DistributionId",
                table: "Items",
                column: "DistributionId",
                principalTable: "Distributions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
