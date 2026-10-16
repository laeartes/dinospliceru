using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSpliceJobErrorMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "splice_jobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error_message",
                table: "splice_jobs");
        }
    }
}
