using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Central.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCodigoDaInstalacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo",
                table: "instalacoes",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Linhas que já existiam antes desta coluna (nenhuma tinha código) ganham um
            // código único cada uma, pra não colidir todas em "" quando o índice único abaixo
            // for criado — usa o próprio id (já único) como base, então nunca repete.
            migrationBuilder.Sql("""
                UPDATE instalacoes SET codigo = upper(substring(md5(id) from 1 for 6)) WHERE codigo = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_instalacoes_codigo",
                table: "instalacoes",
                column: "codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_instalacoes_codigo",
                table: "instalacoes");

            migrationBuilder.DropColumn(
                name: "codigo",
                table: "instalacoes");
        }
    }
}
