using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codec",
                table: "videos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolution_height",
                table: "videos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolution_width",
                table: "videos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codec",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "resolution_height",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "resolution_width",
                table: "videos");
        }
    }
}
