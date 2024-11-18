using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBackendApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerification34 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "TransactionHistories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "TransactionHistories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "TransactionHistories");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "TransactionHistories");
        }
    }
}
