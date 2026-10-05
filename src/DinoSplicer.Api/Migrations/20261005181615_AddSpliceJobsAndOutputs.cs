using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSpliceJobsAndOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "splice_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_video_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    segment_value = table.Column<double>(type: "double precision", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_splice_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_splice_jobs_videos_source_video_id",
                        column: x => x.source_video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "splice_outputs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    splice_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    segment_index = table.Column<int>(type: "integer", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_splice_outputs", x => x.id);
                    table.ForeignKey(
                        name: "fk_splice_outputs_splice_jobs_splice_job_id",
                        column: x => x.splice_job_id,
                        principalTable: "splice_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_splice_jobs_source_video_id",
                table: "splice_jobs",
                column: "source_video_id");

            migrationBuilder.CreateIndex(
                name: "ix_splice_jobs_status",
                table: "splice_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_splice_outputs_splice_job_id_segment_index",
                table: "splice_outputs",
                columns: new[] { "splice_job_id", "segment_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "splice_outputs");

            migrationBuilder.DropTable(
                name: "splice_jobs");
        }
    }
}
