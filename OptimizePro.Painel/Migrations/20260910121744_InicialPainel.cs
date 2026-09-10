using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Painel.Migrations
{
    /// <inheritdoc />
    public partial class InicialPainel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "painel_usuarios",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    login = table.Column<string>(type: "TEXT", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    senha_hash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    senha_sal = table.Column<byte[]>(type: "BLOB", nullable: false),
                    eh_administrador = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    habilitado = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    modulos_liberados = table.Column<string>(type: "TEXT", nullable: false),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ultimo_login_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_painel_usuarios", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_painel_usuarios_login",
                table: "painel_usuarios",
                column: "login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "painel_usuarios");
        }
    }
}
