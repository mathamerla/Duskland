using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duskland.Modules.Products.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ProductStorePriceIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreId_Price_Id",
                schema: "product",
                table: "Products",
                columns: new[] { "StoreId", "Price", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_StoreId_Price_Id",
                schema: "product",
                table: "Products");
        }
    }
}
