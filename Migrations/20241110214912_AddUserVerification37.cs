using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBackendApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerification37 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gain",
                table: "TransactionHistories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gain",
                table: "TransactionHistories");
        }
    }
}
