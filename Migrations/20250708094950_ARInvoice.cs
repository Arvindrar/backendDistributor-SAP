using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backendDistributor.Migrations
{
    /// <inheritdoc />
    public partial class ARInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ARInvoiceNumberTrackers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastUsedNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARInvoiceNumberTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ARInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ARInvoiceNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SalesOrderNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomerRefNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillToAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ARInvoiceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ARInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARInvoiceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ARInvoiceAttachments_ARInvoices_ARInvoiceId",
                        column: x => x.ARInvoiceId,
                        principalTable: "ARInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ARInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ARInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UOM = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WarehouseLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ARInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ARInvoiceItems_ARInvoices_ARInvoiceId",
                        column: x => x.ARInvoiceId,
                        principalTable: "ARInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ARInvoiceNumberTrackers",
                columns: new[] { "Id", "LastUsedNumber" },
                values: new object[] { 3, 1000000 });

            migrationBuilder.CreateIndex(
                name: "IX_ARInvoiceAttachments_ARInvoiceId",
                table: "ARInvoiceAttachments",
                column: "ARInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ARInvoiceItems_ARInvoiceId",
                table: "ARInvoiceItems",
                column: "ARInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ARInvoiceAttachments");

            migrationBuilder.DropTable(
                name: "ARInvoiceItems");

            migrationBuilder.DropTable(
                name: "ARInvoiceNumberTrackers");

            migrationBuilder.DropTable(
                name: "ARInvoices");
        }
    }
}
