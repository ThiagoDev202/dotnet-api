using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    available_quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_available_quantity", "available_quantity >= 0");
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                table: "order_items",
                column: "order_id");

            // Índices para filtros frequentes e evitar N+1 de busca por cliente/status/data
            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_at",
                table: "orders",
                column: "created_at");

            // Role de aplicação com acesso mínimo (sem permissão de DDL)
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'orders_app') THEN
                        CREATE ROLE orders_app LOGIN PASSWORD 'orders_app_password';
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA public TO orders_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON orders, order_items, products TO orders_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO orders_app;
                """);

            // RLS em orders: cliente vê só seus pedidos; admin vê tudo
            migrationBuilder.Sql("""
                ALTER TABLE orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE orders FORCE ROW LEVEL SECURITY;

                CREATE POLICY customer_isolation ON orders
                    USING (
                        customer_id::text = current_setting('app.current_customer_id', true)
                        OR current_setting('app.is_admin', true) = 'true'
                    );
                """);

            // RLS em order_items: acesso derivado dos pedidos do cliente
            migrationBuilder.Sql("""
                ALTER TABLE order_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE order_items FORCE ROW LEVEL SECURITY;

                CREATE POLICY customer_isolation ON order_items
                    USING (
                        order_id IN (
                            SELECT id FROM orders
                             WHERE customer_id::text = current_setting('app.current_customer_id', true)
                                OR current_setting('app.is_admin', true) = 'true'
                        )
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS customer_isolation ON order_items;
                ALTER TABLE order_items DISABLE ROW LEVEL SECURITY;

                DROP POLICY IF EXISTS customer_isolation ON orders;
                ALTER TABLE orders DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
