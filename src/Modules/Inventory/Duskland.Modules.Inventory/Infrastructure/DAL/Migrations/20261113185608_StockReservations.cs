using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duskland.Modules.Inventory.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class StockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockReservations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                    table.CheckConstraint("CK_StockReservations_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockReservations_Stocks_StockId",
                        column: x => x.StockId,
                        principalSchema: "inventory",
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_Status_ExpirationDate",
                schema: "inventory",
                table: "StockReservations",
                columns: new[] { "Status", "ExpirationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_StockId",
                schema: "inventory",
                table: "StockReservations",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_StoreId_ProductId_OrderId",
                schema: "inventory",
                table: "StockReservations",
                columns: new[] { "StoreId", "ProductId", "OrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockReservations",
                schema: "inventory");
        }
    }
}
