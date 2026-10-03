using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DinoSplicer.Api.Migrations
{
    /// <inheritdoc />
    public partial class UseSnakeCaseNamingConvention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_videos",
                table: "videos");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "videos",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "videos",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SizeBytes",
                table: "videos",
                newName: "size_bytes");

            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                table: "videos",
                newName: "original_file_name");

            migrationBuilder.RenameColumn(
                name: "FileName",
                table: "videos",
                newName: "file_name");

            migrationBuilder.RenameColumn(
                name: "DurationSeconds",
                table: "videos",
                newName: "duration_seconds");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "videos",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ContentType",
                table: "videos",
                newName: "content_type");

            migrationBuilder.RenameIndex(
                name: "IX_videos_Status",
                table: "videos",
                newName: "ix_videos_status");

            migrationBuilder.AddPrimaryKey(
                name: "pk_videos",
                table: "videos",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_videos",
                table: "videos");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "videos",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "videos",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "size_bytes",
                table: "videos",
                newName: "SizeBytes");

            migrationBuilder.RenameColumn(
                name: "original_file_name",
                table: "videos",
                newName: "OriginalFileName");

            migrationBuilder.RenameColumn(
                name: "file_name",
                table: "videos",
                newName: "FileName");

            migrationBuilder.RenameColumn(
                name: "duration_seconds",
                table: "videos",
                newName: "DurationSeconds");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "videos",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "content_type",
                table: "videos",
                newName: "ContentType");

            migrationBuilder.RenameIndex(
                name: "ix_videos_status",
                table: "videos",
                newName: "IX_videos_Status");

            migrationBuilder.AddPrimaryKey(
                name: "PK_videos",
                table: "videos",
                column: "Id");
        }
    }
}
