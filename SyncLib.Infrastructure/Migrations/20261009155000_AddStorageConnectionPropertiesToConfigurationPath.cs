using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyncLib.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageConnectionPropertiesToConfigurationPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionType",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.AddColumn<string>(
                name: "ServerHost",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ServerPort",
                table: "ConfigurationPaths",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionType",
                table: "ConfigurationPaths");

            migrationBuilder.DropColumn(
                name: "ServerHost",
                table: "ConfigurationPaths");

            migrationBuilder.DropColumn(
                name: "ServerPort",
                table: "ConfigurationPaths");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "ConfigurationPaths");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "ConfigurationPaths");
        }
    }
}
