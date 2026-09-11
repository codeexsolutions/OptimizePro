using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Central.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaChavesDeMaquina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chave_de_api_hash",
                table: "instalacoes");

            migrationBuilder.CreateTable(
                name: "chaves_de_maquina",
                columns: table => new
                {
                    instalacao_id = table.Column<string>(type: "text", nullable: false),
                    maquina_id = table.Column<string>(type: "text", nullable: false),
                    chave_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chaves_de_maquina", x => new { x.instalacao_id, x.maquina_id });
                    table.ForeignKey(
                        name: "FK_chaves_de_maquina_instalacoes_instalacao_id",
                        column: x => x.instalacao_id,
                        principalTable: "instalacoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chaves_de_maquina");

            migrationBuilder.AddColumn<byte[]>(
                name: "chave_de_api_hash",
                table: "instalacoes",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
