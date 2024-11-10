using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBackendApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerification31 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "DeletedStocks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "DeletedStocks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "DeletedStocks");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "DeletedStocks");
        }
    }
}
