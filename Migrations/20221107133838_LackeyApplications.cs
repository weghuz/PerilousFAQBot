using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FAQBot.Migrations
{
    public partial class LackeyApplications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LackeyApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicantId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ApplicationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LackeyApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LackeyApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimeStamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Approver = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    LackeyApplicationId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LackeyApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LackeyApprovals_LackeyApplications_LackeyApplicationId",
                        column: x => x.LackeyApplicationId,
                        principalTable: "LackeyApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LackeyApprovals_LackeyApplicationId",
                table: "LackeyApprovals",
                column: "LackeyApplicationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LackeyApprovals");

            migrationBuilder.DropTable(
                name: "LackeyApplications");
        }
    }
}
