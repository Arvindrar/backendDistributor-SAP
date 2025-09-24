using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backendDistributor.Migrations
{
    /// <inheritdoc />
    public partial class Addsalesemployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SalesEmployees_Name",
                table: "SalesEmployees",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesEmployees_Name",
                table: "SalesEmployees");
        }
    }
}
