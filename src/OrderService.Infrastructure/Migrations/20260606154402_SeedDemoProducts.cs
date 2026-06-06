using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrderService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "products",
                columns: new[] { "id", "available_quantity", "name", "unit_price" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), 100, "Caneta Azul", 10.00m },
                    { new Guid("22222222-2222-2222-2222-222222222222"), 100, "Caderno Universitário", 25.50m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.DeleteData(
                table: "products",
                keyColumn: "id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"));
        }
    }
}
