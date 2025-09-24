using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backendDistributor.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureUOMGroupPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "UOMGroups",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_UOMGroups_Code",
                table: "UOMGroups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UOMGroups_Code",
                table: "UOMGroups");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "UOMGroups");
        }
    }
}
