namespace OptimizePro.Core.Moldes.Dxf;

/// <summary>
/// Parser recursivo-descendente sobre a lista plana de <see cref="DxfPar"/>: separa
/// as seções HEADER/BLOCKS/ENTITIES (demais seções são ignoradas) e monta a árvore de
/// entidades — POLYLINE absorve os VERTEX/SEQEND seguintes como filhos; BLOCK absorve
/// tudo até ENDBLK.
/// </summary>
public static class DxfAnalisadorDeSecoes
{
    public static DxfDocumento Analisar(IReadOnlyList<DxfPar> pares)
    {
        var documento = new DxfDocumento();
        var indice = 0;

        while (indice < pares.Count)
        {
            var par = pares[indice];

            if (par.Codigo == 0 && par.Valor == "SECTION")
            {
                indice++;
                if (indice >= pares.Count)
                    break;

                var nomeSecao = pares[indice].Codigo == 2 ? pares[indice].Valor : "";
                indice++;

                switch (nomeSecao)
                {
                    case "HEADER":
                        AnalisarHeader(pares, ref indice, documento.VariaveisDeHeader);
                        break;
                    case "BLOCKS":
                        AnalisarBlocks(pares, ref indice, documento.Blocos);
                        break;
                    case "ENTITIES":
                        documento.EntidadesTopo.AddRange(AnalisarEntidades(pares, ref indice, "ENDSEC"));
                        break;
                    default:
                        PularSecao(pares, ref indice);
                        break;
                }
            }
            else
            {
                indice++;
            }
        }

        return documento;
    }

    private static void PularSecao(IReadOnlyList<DxfPar> pares, ref int indice)
    {
        while (indice < pares.Count && !EhTerminador(pares[indice], "ENDSEC"))
            indice++;
        if (indice < pares.Count)
            indice++;
    }

    private static void AnalisarHeader(IReadOnlyList<DxfPar> pares, ref int indice, Dictionary<string, string> variaveis)
    {
        while (indice < pares.Count && !EhTerminador(pares[indice], "ENDSEC"))
        {
            if (pares[indice].Codigo == 9)
            {
                var nome = pares[indice].Valor;
                indice++;
                if (indice < pares.Count)
                {
                    variaveis[nome] = pares[indice].Valor;
                    indice++;
                }
            }
            else
            {
                indice++;
            }
        }

        if (indice < pares.Count)
            indice++;
    }

    private static void AnalisarBlocks(IReadOnlyList<DxfPar> pares, ref int indice, Dictionary<string, DxfEntidadeBruta> blocos)
    {
        while (indice < pares.Count && !EhTerminador(pares[indice], "ENDSEC"))
        {
            if (pares[indice].Codigo == 0 && pares[indice].Valor == "BLOCK")
            {
                var bloco = new DxfEntidadeBruta("BLOCK");
                indice++;

                while (indice < pares.Count && pares[indice].Codigo != 0)
                {
                    bloco.Grupos.Add(pares[indice]);
                    indice++;
                }

                var filhos = AnalisarEntidades(pares, ref indice, "ENDBLK");
                bloco.Filhos.AddRange(filhos);

                var nome = bloco.Primeiro(2);
                if (!string.IsNullOrEmpty(nome))
                    blocos[nome] = bloco;
            }
            else
            {
                indice++;
            }
        }

        if (indice < pares.Count)
            indice++;
    }

    private static List<DxfEntidadeBruta> AnalisarEntidades(IReadOnlyList<DxfPar> pares, ref int indice, string terminador)
    {
        var entidades = new List<DxfEntidadeBruta>();

        while (indice < pares.Count && !EhTerminador(pares[indice], terminador))
        {
            if (pares[indice].Codigo != 0)
            {
                indice++;
                continue;
            }

            var tipo = pares[indice].Valor;
            indice++;

            var entidade = new DxfEntidadeBruta(tipo);
            while (indice < pares.Count && pares[indice].Codigo != 0)
            {
                entidade.Grupos.Add(pares[indice]);
                indice++;
            }

            if (tipo == "POLYLINE")
            {
                while (indice < pares.Count && pares[indice].Codigo == 0 && pares[indice].Valor == "VERTEX")
                {
                    indice++;
                    var vertice = new DxfEntidadeBruta("VERTEX");
                    while (indice < pares.Count && pares[indice].Codigo != 0)
                    {
                        vertice.Grupos.Add(pares[indice]);
                        indice++;
                    }
                    entidade.Filhos.Add(vertice);
                }

                if (indice < pares.Count && pares[indice].Codigo == 0 && pares[indice].Valor == "SEQEND")
                    indice++;
            }

            entidades.Add(entidade);
        }

        if (indice < pares.Count)
            indice++;

        return entidades;
    }

    private static bool EhTerminador(DxfPar par, string terminador) => par.Codigo == 0 && par.Valor == terminador;
}
