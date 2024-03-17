using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class fk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Distributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    IsShop = table.Column<bool>(type: "INTEGER", nullable: true),
                    DontSpawnAmmo = table.Column<bool>(type: "INTEGER", nullable: true),
                    MaxMap = table.Column<int>(type: "INTEGER", nullable: true),
                    StashChance = table.Column<int>(type: "INTEGER", nullable: true),
                    FillRand = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemRolls = table.Column<long>(type: "INTEGER", nullable: true),
                    JunkRolls = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Containers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DistributionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Procedural = table.Column<bool>(type: "INTEGER", nullable: true),
                    DontSpawnAmmo = table.Column<bool>(type: "INTEGER", nullable: true),
                    FillRand = table.Column<bool>(type: "INTEGER", nullable: true),
                    ItemRolls = table.Column<int>(type: "INTEGER", nullable: true),
                    JunkRolls = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Containers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Containers_Distributions_DistributionId",
                        column: x => x.DistributionId,
                        principalTable: "Distributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: true),
                    DistributionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Chance = table.Column<double>(type: "REAL", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Items_Distributions_DistributionId",
                        column: x => x.DistributionId,
                        principalTable: "Distributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcListEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Min = table.Column<int>(type: "INTEGER", nullable: true),
                    Max = table.Column<int>(type: "INTEGER", nullable: true),
                    WheightChance = table.Column<int>(type: "INTEGER", nullable: true),
                    ForceForTiles = table.Column<string>(type: "TEXT", nullable: true),
                    ForceForRooms = table.Column<string>(type: "TEXT", nullable: true),
                    ForceForZones = table.Column<string>(type: "TEXT", nullable: true),
                    ForceForItems = table.Column<string>(type: "TEXT", nullable: true),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcListEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcListEntry_Containers_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Containers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Containers_DistributionId",
                table: "Containers",
                column: "DistributionId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ContainerId",
                table: "Items",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_DistributionId",
                table: "Items",
                column: "DistributionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcListEntry_ContainerId",
                table: "ProcListEntry",
                column: "ContainerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "ProcListEntry");

            migrationBuilder.DropTable(
                name: "Containers");

            migrationBuilder.DropTable(
                name: "Distributions");
        }
    }
}
