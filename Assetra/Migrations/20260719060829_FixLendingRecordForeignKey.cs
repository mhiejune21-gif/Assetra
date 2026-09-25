using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assetra.Migrations
{
    /// <inheritdoc />
    public partial class FixLendingRecordForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LendingRecords_Users_UserId",
                table: "LendingRecords");

            migrationBuilder.DropIndex(
                name: "IX_LendingRecords_UserId",
                table: "LendingRecords");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "LendingRecords");

            migrationBuilder.CreateIndex(
                name: "IX_LendingRecords_BorrowedBy",
                table: "LendingRecords",
                column: "BorrowedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_LendingRecords_Users_BorrowedBy",
                table: "LendingRecords",
                column: "BorrowedBy",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LendingRecords_Users_BorrowedBy",
                table: "LendingRecords");

            migrationBuilder.DropIndex(
                name: "IX_LendingRecords_BorrowedBy",
                table: "LendingRecords");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "LendingRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LendingRecords_UserId",
                table: "LendingRecords",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LendingRecords_Users_UserId",
                table: "LendingRecords",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
