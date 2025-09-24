using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backendDistributor.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IGST",
                table: "TaxDeclarations");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "SalesEmployees");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "SalesEmployees");

            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "SalesEmployees");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "SalesEmployees");

            migrationBuilder.DropColumn(
                name: "Address1",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Address2",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Employee",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Gstin",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "MailId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PostBox",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Customers",
                newName: "CardCode");

            migrationBuilder.AlterColumn<int>(
                name: "ShippingType",
                table: "Customers",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CardName",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FederalTaxID",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupCode",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone1",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SalesPersonCode",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Territory",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Customers_CardCode",
                table: "Customers",
                column: "CardCode");

            migrationBuilder.CreateTable(
                name: "BPAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BPCode = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    AddressType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AddressName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Street = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Block = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GstType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GSTIN = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BPAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BPAddresses_Customers_BPCode",
                        column: x => x.BPCode,
                        principalTable: "Customers",
                        principalColumn: "CardCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_CardCode",
                table: "Customers",
                column: "CardCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BPAddresses_BPCode",
                table: "BPAddresses",
                column: "BPCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BPAddresses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Customers_CardCode",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_CardCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CardName",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "FederalTaxID",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "GroupCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Phone1",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "SalesPersonCode",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Territory",
                table: "Customers");

            migrationBuilder.RenameColumn(
                name: "CardCode",
                table: "Customers",
                newName: "Code");

            migrationBuilder.AddColumn<decimal>(
                name: "IGST",
                table: "TaxDeclarations",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "SalesEmployees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "SalesEmployees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "SalesEmployees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "SalesEmployees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ShippingType",
                table: "Customers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address1",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address2",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Customers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Customers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Employee",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gstin",
                table: "Customers",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailId",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PostBox",
                table: "Customers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Customers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
