using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class InicialOptimize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "encaixe_guardados",
                columns: table => new
                {
                    chave = table.Column<string>(type: "TEXT", nullable: false),
                    assinatura = table.Column<string>(type: "TEXT", nullable: true),
                    largura_tecido = table.Column<double>(type: "REAL", nullable: true),
                    espaco = table.Column<double>(type: "REAL", nullable: true),
                    margem = table.Column<double>(type: "REAL", nullable: true),
                    consumo = table.Column<double>(type: "REAL", nullable: false),
                    aproveitamento = table.Column<double>(type: "REAL", nullable: true),
                    pecas = table.Column<string>(type: "TEXT", nullable: true),
                    posicoes = table.Column<string>(type: "TEXT", nullable: false),
                    receita = table.Column<string>(type: "TEXT", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encaixe_guardados", x => x.chave);
                });

            migrationBuilder.CreateTable(
                name: "encaixe_historico",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    assinatura = table.Column<string>(type: "TEXT", nullable: false),
                    largura_tecido = table.Column<double>(type: "REAL", nullable: true),
                    pecas = table.Column<int>(type: "INTEGER", nullable: true),
                    consumo = table.Column<double>(type: "REAL", nullable: true),
                    aproveitamento = table.Column<double>(type: "REAL", nullable: true),
                    receita = table.Column<string>(type: "TEXT", nullable: true),
                    tentativas = table.Column<int>(type: "INTEGER", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encaixe_historico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "encaixe_receitas",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    assinatura = table.Column<string>(type: "TEXT", nullable: false),
                    receita = table.Column<string>(type: "TEXT", nullable: false),
                    usos = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    vitorias = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encaixe_receitas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "moldes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    observacoes = table.Column<string>(type: "TEXT", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moldes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projeto_clientes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    observacoes = table.Column<string>(type: "TEXT", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projeto_clientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "molde_artes",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    molde_id = table.Column<int>(type: "INTEGER", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_molde_artes", x => x.id);
                    table.ForeignKey(
                        name: "FK_molde_artes_moldes_molde_id",
                        column: x => x.molde_id,
                        principalTable: "moldes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "molde_pecas",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    molde_id = table.Column<int>(type: "INTEGER", nullable: false),
                    tamanho = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "único"),
                    papel = table.Column<string>(type: "TEXT", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: true),
                    quantidade = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    largura = table.Column<double>(type: "REAL", nullable: false),
                    altura = table.Column<double>(type: "REAL", nullable: false),
                    contorno = table.Column<string>(type: "TEXT", nullable: false),
                    furos = table.Column<string>(type: "TEXT", nullable: true),
                    origem = table.Column<string>(type: "TEXT", nullable: true),
                    ordem = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_molde_pecas", x => x.id);
                    table.ForeignKey(
                        name: "FK_molde_pecas_moldes_molde_id",
                        column: x => x.molde_id,
                        principalTable: "moldes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projetos",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    cliente_id = table.Column<int>(type: "INTEGER", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    observacoes = table.Column<string>(type: "TEXT", nullable: true),
                    largura_tecido = table.Column<double>(type: "REAL", nullable: true),
                    espaco = table.Column<double>(type: "REAL", nullable: true),
                    margem = table.Column<double>(type: "REAL", nullable: true),
                    giro = table.Column<string>(type: "TEXT", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projetos", x => x.id);
                    table.ForeignKey(
                        name: "FK_projetos_projeto_clientes_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "projeto_clientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "molde_arte_pecas",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    arte_id = table.Column<int>(type: "INTEGER", nullable: false),
                    papel = table.Column<string>(type: "TEXT", nullable: false),
                    arquivo = table.Column<string>(type: "TEXT", nullable: false),
                    nome_original = table.Column<string>(type: "TEXT", nullable: true),
                    ajuste = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_molde_arte_pecas", x => x.id);
                    table.ForeignKey(
                        name: "FK_molde_arte_pecas_molde_artes_arte_id",
                        column: x => x.arte_id,
                        principalTable: "molde_artes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projeto_pecas",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    projeto_id = table.Column<int>(type: "INTEGER", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    arquivo = table.Column<string>(type: "TEXT", nullable: false),
                    largura = table.Column<double>(type: "REAL", nullable: false),
                    altura = table.Column<double>(type: "REAL", nullable: false),
                    quantidade = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    ordem = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    miniatura = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projeto_pecas", x => x.id);
                    table.ForeignKey(
                        name: "FK_projeto_pecas_projetos_projeto_id",
                        column: x => x.projeto_id,
                        principalTable: "projetos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_encaixe_receitas_assinatura_receita",
                table: "encaixe_receitas",
                columns: new[] { "assinatura", "receita" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_molde_arte_pecas_arte_id",
                table: "molde_arte_pecas",
                column: "arte_id");

            migrationBuilder.CreateIndex(
                name: "IX_molde_artes_molde_id",
                table: "molde_artes",
                column: "molde_id");

            migrationBuilder.CreateIndex(
                name: "IX_molde_pecas_molde_id",
                table: "molde_pecas",
                column: "molde_id");

            migrationBuilder.CreateIndex(
                name: "idx_projeto_pecas_projeto",
                table: "projeto_pecas",
                column: "projeto_id");

            migrationBuilder.CreateIndex(
                name: "idx_projetos_cliente",
                table: "projetos",
                column: "cliente_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encaixe_guardados");

            migrationBuilder.DropTable(
                name: "encaixe_historico");

            migrationBuilder.DropTable(
                name: "encaixe_receitas");

            migrationBuilder.DropTable(
                name: "molde_arte_pecas");

            migrationBuilder.DropTable(
                name: "molde_pecas");

            migrationBuilder.DropTable(
                name: "projeto_pecas");

            migrationBuilder.DropTable(
                name: "molde_artes");

            migrationBuilder.DropTable(
                name: "projetos");

            migrationBuilder.DropTable(
                name: "moldes");

            migrationBuilder.DropTable(
                name: "projeto_clientes");
        }
    }
}
