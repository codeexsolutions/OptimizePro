using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaRedeDeReceitas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "features",
                table: "encaixe_historico",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "placar",
                table: "encaixe_historico",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "encaixe_rede_pesos",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    pesos = table.Column<string>(type: "TEXT", nullable: false),
                    exemplos = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encaixe_rede_pesos", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encaixe_rede_pesos");

            migrationBuilder.DropColumn(
                name: "features",
                table: "encaixe_historico");

            migrationBuilder.DropColumn(
                name: "placar",
                table: "encaixe_historico");
        }
    }
}
