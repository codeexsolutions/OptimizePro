using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAssinaturaDeHistoricoNaMaquina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ultima_assinatura_historico",
                table: "maquinas",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ultima_assinatura_historico",
                table: "maquinas");
        }
    }
}
