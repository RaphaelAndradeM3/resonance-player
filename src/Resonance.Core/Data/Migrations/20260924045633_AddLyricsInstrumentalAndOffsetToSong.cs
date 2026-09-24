using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resonance.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLyricsInstrumentalAndOffsetToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInstrumental",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LyricsOffsetMs",
                table: "Songs",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInstrumental",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "LyricsOffsetMs",
                table: "Songs");
        }
    }
}
