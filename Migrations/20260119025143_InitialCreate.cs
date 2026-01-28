using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace portscanner_backend.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Branch_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Branch_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Branch_cidr = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Branch_id);
                });

            migrationBuilder.CreateTable(
                name: "PortGroups",
                columns: table => new
                {
                    Pg_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pg_name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortGroups", x => x.Pg_id);
                });

            migrationBuilder.CreateTable(
                name: "IpAddresses",
                columns: table => new
                {
                    Ip_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ip_branchId = table.Column<int>(type: "int", nullable: false),
                    Ip_address = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpAddresses", x => x.Ip_id);
                    table.ForeignKey(
                        name: "FK_IpAddresses_Branches_Ip_branchId",
                        column: x => x.Ip_branchId,
                        principalTable: "Branches",
                        principalColumn: "Branch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ports",
                columns: table => new
                {
                    Port_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Port_branchId = table.Column<int>(type: "int", nullable: false),
                    Port_number = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ports", x => x.Port_id);
                    table.ForeignKey(
                        name: "FK_Ports_Branches_Port_branchId",
                        column: x => x.Port_branchId,
                        principalTable: "Branches",
                        principalColumn: "Branch_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortMasters",
                columns: table => new
                {
                    Pm_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pm_portGroup = table.Column<int>(type: "int", nullable: false),
                    Pm_portNumber = table.Column<int>(type: "int", nullable: false),
                    Pm_desc = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortMasters", x => x.Pm_id);
                    table.ForeignKey(
                        name: "FK_PortMasters_PortGroups_Pm_portGroup",
                        column: x => x.Pm_portGroup,
                        principalTable: "PortGroups",
                        principalColumn: "Pg_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanResults",
                columns: table => new
                {
                    Res_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Res_ipAddressId = table.Column<int>(type: "int", nullable: false),
                    Res_portId = table.Column<int>(type: "int", nullable: false),
                    Res_status = table.Column<bool>(type: "bit", nullable: false),
                    Res_scanDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanResults", x => x.Res_id);
                    table.ForeignKey(
                        name: "FK_ScanResults_IpAddresses_Res_ipAddressId",
                        column: x => x.Res_ipAddressId,
                        principalTable: "IpAddresses",
                        principalColumn: "Ip_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScanResults_Ports_Res_portId",
                        column: x => x.Res_portId,
                        principalTable: "Ports",
                        principalColumn: "Port_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IpAddresses_Ip_branchId",
                table: "IpAddresses",
                column: "Ip_branchId");

            migrationBuilder.CreateIndex(
                name: "IX_PortMasters_Pm_portGroup",
                table: "PortMasters",
                column: "Pm_portGroup");

            migrationBuilder.CreateIndex(
                name: "IX_Ports_Port_branchId",
                table: "Ports",
                column: "Port_branchId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_Res_ipAddressId",
                table: "ScanResults",
                column: "Res_ipAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_Res_portId",
                table: "ScanResults",
                column: "Res_portId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortMasters");

            migrationBuilder.DropTable(
                name: "ScanResults");

            migrationBuilder.DropTable(
                name: "PortGroups");

            migrationBuilder.DropTable(
                name: "IpAddresses");

            migrationBuilder.DropTable(
                name: "Ports");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
