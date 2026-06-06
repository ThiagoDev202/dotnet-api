using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GrantTokenTablesToAppRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // refresh_tokens e revoked_tokens foram criadas depois do GRANT inicial,
            // então a role de runtime (orders_app) não tinha acesso a elas. Sem este
            // GRANT, conectar a API como orders_app quebra os fluxos de auth/refresh/logout.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE
                ON refresh_tokens, revoked_tokens
                TO orders_app;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                REVOKE SELECT, INSERT, UPDATE, DELETE
                ON refresh_tokens, revoked_tokens
                FROM orders_app;
            """);
        }
    }
}
