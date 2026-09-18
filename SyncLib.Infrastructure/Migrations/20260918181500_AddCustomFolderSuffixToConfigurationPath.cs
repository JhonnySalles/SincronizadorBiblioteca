using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyncLib.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomFolderSuffixToConfigurationPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFolderSuffix",
                table: "ConfigurationPaths",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomFolderSuffix",
                table: "ConfigurationPaths");
        }
    }
}
