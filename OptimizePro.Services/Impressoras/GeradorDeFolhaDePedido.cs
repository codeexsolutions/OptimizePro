using System.Linq;
using System.Net;
using System.Text;
using OptimizePro.Data.Entidades;
using QRCoder;

namespace OptimizePro.Services.Impressoras;

/// <summary>
/// Porte (bem reduzido) da folha impressa de <c>routes/print-list</c> — a lista de produção
/// com o QR que o aparelho da calandra lê (§22.8). Gera HTML simples pra abrir no navegador
/// padrão; a impressão em si (Ctrl+P) fica com o operador, igual à referência (que também só
/// abre uma aba nova, sem imprimir sozinha).
/// </summary>
public sealed class GeradorDeFolhaDePedido
{
    public string GerarHtml(Pedido pedido)
    {
        var codigoDoPedido = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoPedido, pedido.Id);
        var linhas = new StringBuilder();

        foreach (var item in pedido.Itens.OrderBy(i => i.Posicao))
        {
            var codigoDoItem = CodigoDeQr.GerarCurto(CodigoDeQr.PrefixoRegistro, item.RegistroId);
            linhas.Append($"""
                <tr>
                  <td>{item.Posicao + 1}</td>
                  <td>{Html(item.Tarefa)}<br><small>{Html(item.NomeDoCliente)} · {Html(item.Tecido)}</small></td>
                  <td>{Html(item.NomeDaMaquina)}</td>
                  <td>{(item.ComprimentoDeImpressao ?? 0).ToString("0.00")} m</td>
                  <td><img src="data:image/png;base64,{ImagemBase64(codigoDoItem)}" width="56" height="56" /></td>
                </tr>

                """);
        }

        var idCurto = Html(pedido.Id[..8]);
        var observacao = string.IsNullOrEmpty(pedido.Observacao) ? "" : $" — {Html(pedido.Observacao)}";
        var criadoEm = pedido.CriadoEm.ToString("dd/MM/yyyy HH:mm");
        var qrDoPedido = ImagemBase64(codigoDoPedido);

        return $"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <title>Pedido {idCurto}</title>
            <style>{CssPadrao}</style>
            </head>
            <body>
              <h1>Lista de produção — pedido {idCurto}</h1>
              <p>Criado em {criadoEm}{observacao}</p>
              <table>
                <thead><tr><th>#</th><th>Trabalho</th><th>Máquina</th><th>Metragem</th><th>QR</th></tr></thead>
                <tbody>
                {linhas}
                </tbody>
              </table>
              <div class="rodape">
                <img src="data:image/png;base64,{qrDoPedido}" width="110" height="110" />
                <p>Escaneie este QR pra abrir o pedido inteiro na calandra.<br>{codigoDoPedido}</p>
              </div>
            </body>
            </html>
            """;
    }

    private const string CssPadrao = """
        body { font-family: sans-serif; padding: 24px; color: #111; }
        table { width: 100%; border-collapse: collapse; margin-top: 16px; }
        th, td { border: 1px solid #ccc; padding: 8px; text-align: left; font-size: 13px; vertical-align: middle; }
        .rodape { margin-top: 32px; display: flex; align-items: center; gap: 16px; }
        .rodape p { font-family: monospace; font-size: 12px; color: #555; }
        """;

    private static string ImagemBase64(string conteudo)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(dados);
        return Convert.ToBase64String(png.GetGraphic(8));
    }

    private static string Html(string? valor) => WebUtility.HtmlEncode(valor ?? "");
}
