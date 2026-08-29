using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyncLib.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewPathFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FolderName",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IncludesSubfolders",
                table: "ConfigurationPaths",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "ConfigurationPaths");

            migrationBuilder.DropColumn(
                name: "FolderName",
                table: "ConfigurationPaths");

            migrationBuilder.DropColumn(
                name: "IncludesSubfolders",
                table: "ConfigurationPaths");
        }
    }
}
