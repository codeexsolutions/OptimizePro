namespace OptimizePro.Core.Moldes.Plt;

/// <summary>
/// Decodifica dados do comando <c>PE</c> (polilinha comprimida) — §8.1: "modo 6-bit
/// padrão (códigos 63-126 continuam, 191-254 terminam); troca para modo 5-bit ao
/// encontrar '7'. Bit menos significativo do valor decodificado = sinal."
/// </summary>
/// <remarks>
/// IMPLEMENTAÇÃO NÃO VERIFICADA: este é um formato bit-a-bit raro e a especificação
/// disponível para este porte é uma descrição de alto nível, não o algoritmo original.
/// A leitura abaixo é a interpretação padrão do PE do HP-GL/2 (offsets 63/191, LSB=sinal,
/// deltas relativos salvo prefixo <c>=</c>, gap de pena com <c>&lt;</c>) — validar contra
/// arquivos PLT reais do sistema atual antes de confiar nela; se os contornos saírem
/// deformados/deslocados, é o primeiro lugar a revisar.
/// </remarks>
public static class PltDecodificadorPe
{
    private const int BaseContinuar = 63;
    private const int BaseTerminar = 191;

    internal static void Decodificar(string dados, PltDesenhista desenhista, ICollection<string> avisos)
    {
        var i = 0;
        var n = dados.Length;
        var bits = 6;
        var absolutoNoProximoPonto = false;
        var penaLevantadaNoProximoPonto = false;

        while (i < n)
        {
            var c = dados[i];

            if (c == '7')
            {
                bits = 5;
                i++;
                continue;
            }

            if (c == '=')
            {
                absolutoNoProximoPonto = true;
                i++;
                continue;
            }

            if (c == '<')
            {
                penaLevantadaNoProximoPonto = true;
                i++;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (!DecodificarNumero(dados, ref i, bits, out var dx))
            {
                avisos.Add("PE: dados truncados ao decodificar coordenada X — restante do bloco ignorado.");
                break;
            }

            if (!DecodificarNumero(dados, ref i, bits, out var dy))
            {
                avisos.Add("PE: dados truncados ao decodificar coordenada Y — restante do bloco ignorado.");
                break;
            }

            var coordenadas = new PontoXY(dx, dy);
            var desenhando = !penaLevantadaNoProximoPonto;

            if (absolutoNoProximoPonto)
                desenhista.DesenharSegmentoAbsoluto(coordenadas, desenhando);
            else
                desenhista.MoverRelativo(coordenadas, desenhando);

            absolutoNoProximoPonto = false;
            penaLevantadaNoProximoPonto = false;
        }
    }

    private static bool DecodificarNumero(string dados, ref int i, int bits, out double valor)
    {
        long bruto = 0;
        var deslocamento = 0;
        var mascara = (1 << bits) - 1;

        while (true)
        {
            if (i >= dados.Length)
            {
                valor = 0;
                return false;
            }

            var codigo = dados[i];
            i++;

            var terminador = codigo >= BaseTerminar;
            var digito = (terminador ? codigo - BaseTerminar : codigo - BaseContinuar) & mascara;

            bruto |= (long)digito << deslocamento;
            deslocamento += bits;

            if (terminador)
                break;
        }

        var sinal = (bruto & 1) != 0 ? -1 : 1;
        valor = sinal * (bruto >> 1);
        return true;
    }
}
