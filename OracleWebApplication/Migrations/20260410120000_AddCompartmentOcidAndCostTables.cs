using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OracleWebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddCompartmentOcidAndCostTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompartmentOcid",
                table: "ClientTenants",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OciCostRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientTenantId = table.Column<int>(type: "int", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Service = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Region = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FetchedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciCostRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OciCostRecords_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OciDataTransferRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientTenantId = table.Column<int>(type: "int", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OutboundGb = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    FetchedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OciDataTransferRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OciDataTransferRecords_ClientTenants_ClientTenantId",
                        column: x => x.ClientTenantId,
                        principalTable: "ClientTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OciCostRecords_ClientTenantId_UsageDate",
                table: "OciCostRecords",
                columns: new[] { "ClientTenantId", "UsageDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OciCostRecords_Region",
                table: "OciCostRecords",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_OciCostRecords_Service",
                table: "OciCostRecords",
                column: "Service");

            migrationBuilder.CreateIndex(
                name: "IX_OciDataTransferRecords_ClientTenantId_UsageDate",
                table: "OciDataTransferRecords",
                columns: new[] { "ClientTenantId", "UsageDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OciCostRecords");

            migrationBuilder.DropTable(
                name: "OciDataTransferRecords");

            migrationBuilder.DropColumn(
                name: "CompartmentOcid",
                table: "ClientTenants");
        }
    }
}
