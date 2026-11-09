using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCutJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cut_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_video_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_seconds = table.Column<double>(type: "double precision", nullable: false),
                    end_seconds = table.Column<double>(type: "double precision", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    output_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    output_storage_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    output_duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    output_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cut_jobs", x => x.id);
                    table.CheckConstraint("ck_cut_jobs_valid_range", "start_seconds >= 0 AND end_seconds > start_seconds");
                    table.ForeignKey(
                        name: "fk_cut_jobs_videos_source_video_id",
                        column: x => x.source_video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cut_jobs_source_video_id",
                table: "cut_jobs",
                column: "source_video_id");

            migrationBuilder.CreateIndex(
                name: "ix_cut_jobs_status",
                table: "cut_jobs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cut_jobs");
        }
    }
}
