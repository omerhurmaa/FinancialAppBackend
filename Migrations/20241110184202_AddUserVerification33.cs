using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBackendApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerification33 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "Stocks");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedStocks_UserId",
                table: "DeletedStocks",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeletedStocks_Users_UserId",
                table: "DeletedStocks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeletedStocks_Users_UserId",
                table: "DeletedStocks");

            migrationBuilder.DropIndex(
                name: "IX_DeletedStocks_UserId",
                table: "DeletedStocks");

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "Stocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "Stocks",
                type: "numeric",
                nullable: true);
        }
    }
}
