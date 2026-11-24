using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompressionTargetSizeBytes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "target_size_bytes",
                table: "compression_jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_compression_jobs_positive_target_file_size",
                table: "compression_jobs",
                sql: "target_size_bytes IS NULL OR target_size_bytes > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_compression_jobs_positive_target_file_size",
                table: "compression_jobs");

            migrationBuilder.DropColumn(
                name: "target_size_bytes",
                table: "compression_jobs");
        }
    }
}
