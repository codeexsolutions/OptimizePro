using System.Text;

namespace OptimizePro.Core.Moldes.Plt;

/// <summary>
/// Leitor de arquivos PLT/HP-GL — §4.2/§8.1 do documento de arquitetura. Interpreta o
/// fluxo de comandos como um "plotter" (PU/PD/PA/PR/AA/AR/CI/PE/LB), depois costura e
/// classifica em peças/furos via <see cref="MontagemDeMoldes"/>, igual ao leitor DXF.
/// </summary>
public sealed class LeitorPlt : ILeitorDeMolde
{
    public bool SuportaExtensao(string extensao)
    {
        var semPonto = extensao.TrimStart('.');
        return string.Equals(semPonto, "plt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(semPonto, "hpgl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(semPonto, "hpg", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ResultadoLeituraMolde> LerAsync(Stream conteudo, OpcoesLeituraMolde opcoes)
    {
        var avisos = new List<string>();
        var avisosUnicos = new HashSet<string>();

        string texto;
        using (var leitor = new StreamReader(conteudo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            texto = await leitor.ReadToEndAsync();

        var comandos = PltTokenizador.Tokenizar(texto);

        var desenhista = new PltDesenhista();
        var textos = new List<RotuloTexto>();

        foreach (var comando in comandos)
            ProcessarComando(comando, desenhista, textos, avisosUnicos);

        desenhista.FinalizarTracoAtual();
        avisos.AddRange(avisosUnicos);

        var tracos = desenhista.Tracos;
        if (tracos.Count == 0)
            return new ResultadoLeituraMolde([], "desconhecida", avisos, "Nenhuma geometria reconhecida no arquivo PLT.");

        var caixaGeral = Geometria.CaixaDeContorno([.. tracos.SelectMany(t => t.Pontos)]);
        var maiorLado = Math.Max(caixaGeral.Largura, caixaGeral.Altura);
        var (fatorParaCm, nomeUnidade) = ResolverUnidade(opcoes.UnidadeForcada, maiorLado, avisos);

        // PLT cresce para cima como o DXF; inverte Y para o espaço de trabalho (Y para baixo).
        PontoXY ParaEspacoDeTrabalho(PontoXY p) => new(p.X * fatorParaCm, -p.Y * fatorParaCm);

        var tracosCm = tracos.Select(t => new Traco([.. t.Pontos.Select(ParaEspacoDeTrabalho)], t.Fechada)).ToList();
        var textosCm = textos.Select(r => new RotuloTexto(r.Texto, r.X * fatorParaCm, -r.Y * fatorParaCm)).ToList();

        var lacos = MontagemDeMoldes.MontarLacos(tracosCm);
        var quantidadeAbertos = lacos.Count(l => !l.Fechado);
        if (quantidadeAbertos > 0)
            avisos.Add($"{quantidadeAbertos} traço(s) não formaram contorno fechado e foram ignorados.");

        var pecasBrutas = MontagemDeMoldes.SepararPecasEFuros(lacos);
        var pecas = MontagemDeMoldes.MontarPecasLidas(pecasBrutas, textosCm);

        return new ResultadoLeituraMolde(pecas, nomeUnidade, avisos, null);
    }

    private static void ProcessarComando(PltComando cmd, PltDesenhista desenhista, List<RotuloTexto> textos, HashSet<string> avisos)
    {
        switch (cmd.Opcode)
        {
            case "PU":
            case "PD":
                ProcessarMovimento(cmd, desenhista);
                break;

            case "PA":
                desenhista.ModoAbsoluto = true;
                ProcessarPontosNoModoAtual(cmd, desenhista);
                break;

            case "PR":
                desenhista.ModoAbsoluto = false;
                ProcessarPontosNoModoAtual(cmd, desenhista);
                break;

            case "AA":
                if (cmd.Numeros.Count >= 3)
                    DesenharArco(desenhista, new PontoXY(cmd.Numeros[0], cmd.Numeros[1]), cmd.Numeros[2]);
                break;

            case "AR":
                if (cmd.Numeros.Count >= 3)
                {
                    var centro = new PontoXY(desenhista.Posicao.X + cmd.Numeros[0], desenhista.Posicao.Y + cmd.Numeros[1]);
                    DesenharArco(desenhista, centro, cmd.Numeros[2]);
                }
                break;

            case "CI":
                if (cmd.Numeros.Count >= 1)
                {
                    var pontos = PltCurvas.PontosDoArcoPorSweep(desenhista.Posicao, cmd.Numeros[0], 0, 2 * Math.PI);
                    desenhista.AdicionarTracoFechado(pontos);
                }
                break;

            case "PE":
                PltDecodificadorPe.Decodificar(cmd.Dados ?? "", desenhista, avisos);
                break;

            case "LB":
                if (!string.IsNullOrEmpty(cmd.Dados))
                    textos.Add(new RotuloTexto(cmd.Dados, desenhista.Posicao.X, desenhista.Posicao.Y));
                break;

            case "SC":
            case "IP":
                avisos.Add($"Comando {cmd.Opcode} (escala própria do plotter) não é aplicado — coordenadas usadas em unidade crua.");
                break;
        }
    }

    private static void ProcessarMovimento(PltComando cmd, PltDesenhista desenhista)
    {
        var desenhando = cmd.Opcode == "PD";
        desenhista.DefinirPena(desenhando);
        ProcessarPontosNoModoAtual(cmd, desenhista, desenhando);
    }

    private static void ProcessarPontosNoModoAtual(PltComando cmd, PltDesenhista desenhista, bool? forcarDesenho = null)
    {
        var desenhando = forcarDesenho ?? desenhista.PenaAbaixada;
        for (var i = 0; i + 1 < cmd.Numeros.Count; i += 2)
            desenhista.MoverParaComModo(new PontoXY(cmd.Numeros[i], cmd.Numeros[i + 1]), desenhando);
    }

    private static void DesenharArco(PltDesenhista desenhista, PontoXY centro, double anguloGraus)
    {
        var inicio = desenhista.Posicao;
        var raio = Geometria.DistanciaEntre(inicio, centro);
        if (raio < 1e-9)
            return;

        var anguloInicialRad = Math.Atan2(inicio.Y - centro.Y, inicio.X - centro.X);
        var sweepRad = anguloGraus * Math.PI / 180.0;

        var pontos = PltCurvas.PontosDoArcoPorSweep(centro, raio, anguloInicialRad, sweepRad);
        foreach (var p in pontos.Skip(1))
            desenhista.DesenharSegmentoAbsoluto(p, desenhista.PenaAbaixada);
    }

    /// <summary>
    /// Prioridade: unidade forçada > chute pelo tamanho do desenho testando, em ordem,
    /// [plu, mil, mm, cm] e usando a primeira cujo maior lado cai em 5cm..800cm (§8.1) —
    /// PLT não tem cabeçalho de unidade.
    /// </summary>
    private static (double Fator, string Nome) ResolverUnidade(string? unidadeForcada, double maiorLadoBBox, List<string> avisos)
    {
        if (!string.IsNullOrWhiteSpace(unidadeForcada))
        {
            var forcado = FatorPorNome(unidadeForcada);
            if (forcado is not null)
                return (forcado.Value, unidadeForcada);

            avisos.Add($"Unidade forçada '{unidadeForcada}' não reconhecida — ignorando.");
        }

        (double Fator, string Nome)[] candidatos =
        [
            (2.54 / 1016.0, "plu"),
            (0.00254, "mil"),
            (0.1, "mm"),
            (1.0, "cm"),
        ];

        foreach (var candidato in candidatos)
        {
            var maiorLadoEmCm = maiorLadoBBox * candidato.Fator;
            if (maiorLadoEmCm is >= 5.0 and <= 800.0)
                return candidato;
        }

        avisos.Add("Não foi possível estimar a unidade do PLT pelo tamanho do desenho — usando plu (padrão).");
        return candidatos[0];
    }

    private static double? FatorPorNome(string unidade) => unidade.Trim().ToLowerInvariant() switch
    {
        "plu" => 2.54 / 1016.0,
        "mil" => 0.00254,
        "mm" or "milimetro" or "milímetro" => 0.1,
        "cm" or "centimetro" or "centímetro" => 1.0,
        "m" or "metro" => 100.0,
        "pol" or "polegada" or "in" or "inch" => 2.54,
        _ => null,
    };
}
