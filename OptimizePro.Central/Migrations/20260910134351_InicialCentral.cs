using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Central.Migrations
{
    /// <inheritdoc />
    public partial class InicialCentral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instalacoes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    cliente_id_hash = table.Column<long>(type: "bigint", nullable: false),
                    chave_de_api_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    nome_da_fabrica = table.Column<string>(type: "text", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ultima_sincronizacao_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instalacoes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_instalacoes_cliente_id_hash",
                table: "instalacoes",
                column: "cliente_id_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instalacoes");
        }
    }
}
