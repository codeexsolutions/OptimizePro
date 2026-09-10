using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFrotaDeImpressoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maquinas",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    nome = table.Column<string>(type: "TEXT", nullable: false),
                    tipo = table.Column<string>(type: "TEXT", nullable: false),
                    habilitada = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    host = table.Column<string>(type: "TEXT", nullable: true),
                    ip = table.Column<string>(type: "TEXT", nullable: true),
                    caminho_historico = table.Column<string>(type: "TEXT", nullable: true),
                    pasta_preview = table.Column<string>(type: "TEXT", nullable: true),
                    pasta_log_ao_vivo = table.Column<string>(type: "TEXT", nullable: true),
                    arquivo_log_ao_vivo = table.Column<string>(type: "TEXT", nullable: true),
                    pasta_log_de_status = table.Column<string>(type: "TEXT", nullable: true),
                    caminho_lista_de_trabalhos = table.Column<string>(type: "TEXT", nullable: true),
                    caminho_estatisticas_de_tinta = table.Column<string>(type: "TEXT", nullable: true),
                    origem = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "manual"),
                    posicao = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    descoberta_em = table.Column<DateTime>(type: "TEXT", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maquinas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ordens_de_servico",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    nome_do_cliente = table.Column<string>(type: "TEXT", nullable: false),
                    tecido = table.Column<string>(type: "TEXT", nullable: true),
                    tamanho_de_impressao = table.Column<string>(type: "TEXT", nullable: true),
                    metros = table.Column<double>(type: "REAL", nullable: true),
                    operador = table.Column<string>(type: "TEXT", nullable: true),
                    maquina = table.Column<string>(type: "TEXT", nullable: true),
                    data = table.Column<string>(type: "TEXT", nullable: false),
                    observacao = table.Column<string>(type: "TEXT", nullable: true),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_de_servico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    criado_em = table.Column<DateTime>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "aberto"),
                    observacao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "registros_de_impressao",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    maquina_id = table.Column<string>(type: "TEXT", nullable: false),
                    nome_da_maquina = table.Column<string>(type: "TEXT", nullable: true),
                    tipo_de_origem = table.Column<string>(type: "TEXT", nullable: true),
                    data_hora = table.Column<string>(type: "TEXT", nullable: false),
                    data = table.Column<string>(type: "TEXT", nullable: false),
                    hora = table.Column<string>(type: "TEXT", nullable: true),
                    tarefa = table.Column<string>(type: "TEXT", nullable: true),
                    passada = table.Column<int>(type: "INTEGER", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: true),
                    cancelada = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    com_erro = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    area_de_impressao = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    comprimento_de_impressao = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    metrica_estimada = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    concluido = table.Column<double>(type: "REAL", nullable: true),
                    total = table.Column<double>(type: "REAL", nullable: true),
                    tempo_segundos = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    tinta_ml = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    tinta_experimental = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    canais_de_tinta = table.Column<string>(type: "TEXT", nullable: true),
                    referencia_de_preview = table.Column<string>(type: "TEXT", nullable: true),
                    percentual_de_progresso = table.Column<double>(type: "REAL", nullable: true),
                    estado_do_progresso = table.Column<string>(type: "TEXT", nullable: true),
                    horas_decorridas = table.Column<double>(type: "REAL", nullable: true),
                    eh_recorte_ou_mosaico = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    atualizado_em = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_de_impressao", x => x.id);
                    table.ForeignKey(
                        name: "FK_registros_de_impressao_maquinas_maquina_id",
                        column: x => x.maquina_id,
                        principalTable: "maquinas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ordens_de_servico_imagens",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ordem_id = table.Column<string>(type: "TEXT", nullable: false),
                    nome_do_arquivo = table.Column<string>(type: "TEXT", nullable: true),
                    tipo_mime = table.Column<string>(type: "TEXT", nullable: false),
                    posicao = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    eh_blusa = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    quantidade = table.Column<int>(type: "INTEGER", nullable: true),
                    dados = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_de_servico_imagens", x => x.id);
                    table.ForeignKey(
                        name: "FK_ordens_de_servico_imagens_ordens_de_servico_ordem_id",
                        column: x => x.ordem_id,
                        principalTable: "ordens_de_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pedido_itens",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    pedido_id = table.Column<string>(type: "TEXT", nullable: false),
                    posicao = table.Column<int>(type: "INTEGER", nullable: false),
                    registro_id = table.Column<string>(type: "TEXT", nullable: false),
                    nome_do_cliente = table.Column<string>(type: "TEXT", nullable: true),
                    tecido = table.Column<string>(type: "TEXT", nullable: true),
                    tarefa = table.Column<string>(type: "TEXT", nullable: true),
                    maquina_id = table.Column<string>(type: "TEXT", nullable: true),
                    nome_da_maquina = table.Column<string>(type: "TEXT", nullable: true),
                    comprimento_de_impressao = table.Column<double>(type: "REAL", nullable: true),
                    data = table.Column<string>(type: "TEXT", nullable: true),
                    ordem_id = table.Column<string>(type: "TEXT", nullable: true),
                    status_na_calandra = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "pendente"),
                    motivo_na_calandra = table.Column<string>(type: "TEXT", nullable: true),
                    motivo_personalizado_na_calandra = table.Column<string>(type: "TEXT", nullable: true),
                    data_na_calandra = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedido_itens", x => x.id);
                    table.ForeignKey(
                        name: "FK_pedido_itens_ordens_de_servico_ordem_id",
                        column: x => x.ordem_id,
                        principalTable: "ordens_de_servico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pedido_itens_pedidos_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_maquinas_host",
                table: "maquinas",
                column: "host");

            migrationBuilder.CreateIndex(
                name: "idx_ordens_de_servico_data",
                table: "ordens_de_servico",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "idx_ordens_de_servico_imagens_ordem",
                table: "ordens_de_servico_imagens",
                column: "ordem_id");

            migrationBuilder.CreateIndex(
                name: "idx_pedido_itens_pedido",
                table: "pedido_itens",
                columns: new[] { "pedido_id", "posicao" });

            migrationBuilder.CreateIndex(
                name: "idx_pedido_itens_registro",
                table: "pedido_itens",
                column: "registro_id");

            migrationBuilder.CreateIndex(
                name: "IX_pedido_itens_ordem_id",
                table: "pedido_itens",
                column: "ordem_id");

            migrationBuilder.CreateIndex(
                name: "idx_registros_data",
                table: "registros_de_impressao",
                column: "data");

            migrationBuilder.CreateIndex(
                name: "idx_registros_maquina_data",
                table: "registros_de_impressao",
                columns: new[] { "maquina_id", "data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ordens_de_servico_imagens");

            migrationBuilder.DropTable(
                name: "pedido_itens");

            migrationBuilder.DropTable(
                name: "registros_de_impressao");

            migrationBuilder.DropTable(
                name: "ordens_de_servico");

            migrationBuilder.DropTable(
                name: "pedidos");

            migrationBuilder.DropTable(
                name: "maquinas");
        }
    }
}
