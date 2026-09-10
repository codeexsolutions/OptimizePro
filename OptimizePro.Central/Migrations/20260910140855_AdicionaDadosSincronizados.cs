using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Central.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDadosSincronizados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dados_sincronizados",
                columns: table => new
                {
                    instalacao_id = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    entidade_id = table.Column<string>(type: "text", nullable: false),
                    dados_json = table.Column<string>(type: "jsonb", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dados_sincronizados", x => new { x.instalacao_id, x.tipo, x.entidade_id });
                    table.ForeignKey(
                        name: "FK_dados_sincronizados_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dados_sincronizados_instalacao_id_tipo",
                table: "dados_sincronizados",
                columns: new[] { "instalacao_id", "tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dados_sincronizados");
        }
    }
}
