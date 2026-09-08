using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptimizePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConfiguracaoApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracao_app",
                columns: table => new
                {
                    chave = table.Column<string>(type: "TEXT", nullable: false),
                    valor = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracao_app", x => x.chave);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracao_app");
        }
    }
}
