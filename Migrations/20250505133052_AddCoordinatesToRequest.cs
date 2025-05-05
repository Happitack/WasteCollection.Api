using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WasteCollection.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCoordinatesToRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Requests",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Requests");
        }
    }
}
