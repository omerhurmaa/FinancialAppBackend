using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyBackendApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoritesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BDate",
                table: "PendingUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BDate",
                table: "PendingUsers",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }
    }
}
