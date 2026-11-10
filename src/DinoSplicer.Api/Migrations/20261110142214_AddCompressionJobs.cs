using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCompressionJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compression_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_video_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    video_codec = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    crf = table.Column<int>(type: "integer", nullable: false),
                    target_width = table.Column<int>(type: "integer", nullable: true),
                    target_height = table.Column<int>(type: "integer", nullable: true),
                    audio_bitrate_kbps = table.Column<int>(type: "integer", nullable: true),
                    preset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("pk_compression_jobs", x => x.id);
                    table.CheckConstraint("ck_compression_jobs_audio_bitrate_range", "audio_bitrate_kbps IS NULL OR audio_bitrate_kbps BETWEEN 32 AND 512");
                    table.CheckConstraint("ck_compression_jobs_crf_range", "crf BETWEEN 0 AND 63");
                    table.CheckConstraint("ck_compression_jobs_positive_target_size", "(target_width IS NULL OR target_width > 0) AND (target_height IS NULL OR target_height > 0)");
                    table.ForeignKey(
                        name: "fk_compression_jobs_videos_source_video_id",
                        column: x => x.source_video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_compression_jobs_source_video_id",
                table: "compression_jobs",
                column: "source_video_id");

            migrationBuilder.CreateIndex(
                name: "ix_compression_jobs_status",
                table: "compression_jobs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compression_jobs");
        }
    }
}
