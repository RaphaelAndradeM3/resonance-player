using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resonance.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAcousticFingerprintAndAcoustIdToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcoustId",
                table: "Songs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcousticFingerprint",
                table: "Songs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcoustId",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "AcousticFingerprint",
                table: "Songs");
        }
    }
}
