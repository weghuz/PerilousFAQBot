using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FAQBot.Migrations
{
    public partial class LackeyApplicationsFixes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LackeyApprovals_LackeyApplications_LackeyApplicationId",
                table: "LackeyApprovals");

            migrationBuilder.DropIndex(
                name: "IX_LackeyApprovals_LackeyApplicationId",
                table: "LackeyApprovals");

            migrationBuilder.DropColumn(
                name: "LackeyApplicationId",
                table: "LackeyApprovals");

            migrationBuilder.RenameColumn(
                name: "Approver",
                table: "LackeyApprovals",
                newName: "ApproverId");

            migrationBuilder.AddColumn<int>(
                name: "ApplicationId",
                table: "LackeyApprovals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LackeyApprovals_ApplicationId",
                table: "LackeyApprovals",
                column: "ApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_LackeyApprovals_LackeyApplications_ApplicationId",
                table: "LackeyApprovals",
                column: "ApplicationId",
                principalTable: "LackeyApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LackeyApprovals_LackeyApplications_ApplicationId",
                table: "LackeyApprovals");

            migrationBuilder.DropIndex(
                name: "IX_LackeyApprovals_ApplicationId",
                table: "LackeyApprovals");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "LackeyApprovals");

            migrationBuilder.RenameColumn(
                name: "ApproverId",
                table: "LackeyApprovals",
                newName: "Approver");

            migrationBuilder.AddColumn<int>(
                name: "LackeyApplicationId",
                table: "LackeyApprovals",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LackeyApprovals_LackeyApplicationId",
                table: "LackeyApprovals",
                column: "LackeyApplicationId");

            migrationBuilder.AddForeignKey(
                name: "FK_LackeyApprovals_LackeyApplications_LackeyApplicationId",
                table: "LackeyApprovals",
                column: "LackeyApplicationId",
                principalTable: "LackeyApplications",
                principalColumn: "Id");
        }
    }
}
