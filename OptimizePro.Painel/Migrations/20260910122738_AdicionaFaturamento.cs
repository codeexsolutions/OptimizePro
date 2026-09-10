using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Painel.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFaturamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "painel_faturamento",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    valor_base_mensal = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    valor_por_usuario_extra = table.Column<decimal>(type: "TEXT", nullable: false, defaultValue: 0m),
                    limite_de_usuarios_no_plano = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 7),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_painel_faturamento", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "painel_faturamento");
        }
    }
}
