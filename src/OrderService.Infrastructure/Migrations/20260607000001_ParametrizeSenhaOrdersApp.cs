using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParametrizeSenhaOrdersApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lê a senha da variável de sessão do Postgres (definida pelo startup antes de migrar).
            // Se a variável não estiver definida, mantém a senha atual sem erro.
            migrationBuilder.Sql("""
                DO $$
                DECLARE v_pass text := current_setting('app.orders_app_password', true);
                BEGIN
                    IF v_pass IS NOT NULL AND v_pass <> '' THEN
                        EXECUTE format('ALTER ROLE orders_app PASSWORD %L', v_pass);
                    END IF;
                END
                $$;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Não é possível reverter automaticamente — a senha anterior é desconhecida.
        }
    }
}
