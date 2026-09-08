using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using OptimizePro.Core;
using OptimizePro.Core.Vetor;

namespace OptimizePro.Services.Vetor;

/// <summary>
/// "Plano B" (02/09/2026) — chama o Potrace de verdade (biblioteca madura de traçado
/// bitmap→vetor) rodando como PROCESSO EXTERNO (<c>Ferramentas/VetorGpl</c>), não como
/// dependência in-process. Isso é proposital, não um detalhe de implementação: o Potrace
/// (via BitmapToVector) é GPL-3.0-or-later, e GPL trata como "obra combinada" (que precisa
/// inteira sob GPL) qualquer coisa LINKADA — chamada de função direta, mesmo numa DLL
/// separada, rodando no mesmo processo. Rodar como `.exe` externo, chamado só por
/// <see cref="Process.Start(ProcessStartInfo)"/> com comunicação por arquivo+stdout (nunca
/// por chamada de função), é "mera agregação" — mantém <c>Optimize.App</c> fora do GPL. Ver
/// <c>Ferramentas/VetorGpl/README.md</c> pro detalhe completo (e o aviso de que isso não é
/// aconselhamento jurídico definitivo).
///
/// A quantização de cor e a limpeza de cisco continuam sendo as NOSSAS (já com a correção
/// de "miolo" — ver <see cref="HistogramaDeCores"/>) — só o traçado de contorno+curva de
/// cada camada de cor (a parte que o motor próprio ainda erra em arte complexa) é
/// delegado ao Potrace.
/// </summary>
public sealed class PotraceProcessoService
{
    private readonly string _caminhoDoExecutavel;

    public PotraceProcessoService(string? caminhoDoExecutavel = null)
    {
        _caminhoDoExecutavel = caminhoDoExecutavel
            ?? Path.Combine(AppContext.BaseDirectory, "VetorGpl", "VetorGpl.exe");
    }

    public async Task<ResultadoDeVetorizacao> VetorizarAsync(byte[] imagemBytes, OpcoesDeVetorizacao opcoes, CancellationToken ct = default)
    {
        if (!File.Exists(_caminhoDoExecutavel))
        {
            throw new InvalidOperationException(
                $"Motor externo de vetorização (Potrace) não encontrado em '{_caminhoDoExecutavel}'. " +
                "Ele precisa ser publicado separadamente (Ferramentas/VetorGpl) e copiado pra uma pasta " +
                "'VetorGpl' ao lado do executável do app — não vem embutido por licença (GPL).");
        }

        var imagem = DecodificacaoDeImagem.Decodificar(imagemBytes);

        var quantizacao = QuantizadorDeCores.Quantizar(imagem, new OpcoesDeQuantizacao(opcoes.Cores, opcoes.JuntarSombras));
        var indicesLimpos = LimpezaDeCisco.Limpar(quantizacao.IndicesPorPixel, imagem.Largura, imagem.Altura, opcoes.Detalhe);
        var indicesPorArea = OrdemDePinturaPorArea(indicesLimpos, quantizacao.Paleta.Count);

        var pastaTemp = Path.Combine(Path.GetTempPath(), $"OptimizePro-VetorGpl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pastaTemp);

        try
        {
            var camadas = new List<ConversorSvg.CamadaSvg>();

            foreach (var indice in indicesPorArea)
            {
                ct.ThrowIfCancellationRequested();

                var caminhoMascara = Path.Combine(pastaTemp, $"{indice}.png");
                if (!SalvarMascaraPreteEBranco(imagem, indicesLimpos, indice, caminhoMascara))
                    continue; // cor sem nenhum pixel (pode acontecer após limpeza de cisco)

                var comandosD = await RodarSubprocessoAsync(caminhoMascara, ct);
                if (comandosD.Count == 0)
                    continue;

                camadas.Add(new ConversorSvg.CamadaSvg(string.Join(" ", comandosD), quantizacao.Paleta[indice]));
            }

            var svg = ConversorSvg.MontarSvg(imagem.Largura, imagem.Altura, camadas);
            return new ResultadoDeVetorizacao(svg, imagem.Largura, imagem.Altura);
        }
        finally
        {
            try { Directory.Delete(pastaTemp, recursive: true); } catch { /* best-effort — arquivo temporário, não é crítico */ }
        }
    }

    /// <summary>Índices da paleta ordenados por área (nº de pixels) decrescente — mesma ideia de <see cref="VetorService"/>, pra camada grande nunca ficar por cima de detalhe fino.</summary>
    private static IReadOnlyList<int> OrdemDePinturaPorArea(IReadOnlyList<int> indicesPorPixel, int numeroDeCores)
    {
        var contagem = new int[numeroDeCores];
        foreach (var indice in indicesPorPixel)
            contagem[indice]++;

        var ordem = new int[numeroDeCores];
        for (var i = 0; i < numeroDeCores; i++) ordem[i] = i;

        Array.Sort(ordem, (a, b) => contagem[b].CompareTo(contagem[a]));
        return ordem;
    }

    /// <summary>Preto = forma desta cor (o que o Potrace deve traçar), branco = resto — convenção do BitmapToVector (pixel abaixo de 128 é "preto").</summary>
    private static bool SalvarMascaraPreteEBranco(ImagemRgba imagem, IReadOnlyList<int> indicesPorPixel, int indiceAlvo, string caminhoDeSaida)
    {
        var temPixel = false;
        using var bitmap = new Bitmap(imagem.Largura, imagem.Altura, PixelFormat.Format32bppArgb);
        var dados = bitmap.LockBits(new Rectangle(0, 0, imagem.Largura, imagem.Altura), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var linha = new byte[imagem.Largura * 4];
            for (var y = 0; y < imagem.Altura; y++)
            {
                for (var x = 0; x < imagem.Largura; x++)
                {
                    var ehDaCor = indicesPorPixel[y * imagem.Largura + x] == indiceAlvo;
                    temPixel |= ehDaCor;
                    var v = (byte)(ehDaCor ? 0 : 255); // preto (0) ou branco (255)
                    var i = x * 4;
                    linha[i] = v; linha[i + 1] = v; linha[i + 2] = v; linha[i + 3] = 255;
                }
                Marshal.Copy(linha, 0, dados.Scan0 + y * dados.Stride, linha.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(dados);
        }

        if (!temPixel)
            return false;

        bitmap.Save(caminhoDeSaida, ImageFormat.Png);
        return true;
    }

    private async Task<List<string>> RodarSubprocessoAsync(string caminhoMascara, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_caminhoDoExecutavel)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("trace");
        psi.ArgumentList.Add("--entrada");
        psi.ArgumentList.Add(caminhoMascara);

        using var processo = Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o motor externo de vetorização.");

        var saidaTask = processo.StandardOutput.ReadToEndAsync(ct);
        var erroTask = processo.StandardError.ReadToEndAsync(ct);
        await processo.WaitForExitAsync(ct);

        if (processo.ExitCode != 0)
            throw new InvalidOperationException($"Motor externo de vetorização (Potrace) falhou: {(await erroTask).Trim()}");

        var saida = await saidaTask;
        return JsonSerializer.Deserialize<List<string>>(saida) ?? [];
    }
}
