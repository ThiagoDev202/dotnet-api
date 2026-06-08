using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderService.Infrastructure.Persistence;

#nullable disable

namespace OrderService.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OrderServiceDbContext))]
    [Migration("20260608000001_AddCurrencyToOrderItem")]
    public partial class AddCurrencyToOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unit_price_currency",
                table: "order_items",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "BRL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "unit_price_currency",
                table: "order_items");
        }
    }
}
