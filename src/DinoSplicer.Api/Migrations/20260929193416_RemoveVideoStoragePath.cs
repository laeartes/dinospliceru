using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVideoStoragePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "videos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
