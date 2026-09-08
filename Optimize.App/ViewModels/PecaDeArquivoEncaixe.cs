using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using OptimizePro.Core;
using OptimizePro.Core.Encaixe;

namespace Optimize.App.ViewModels;

/// <summary>
/// Uma peça lida de um arquivo (DXF/PLT/SVG/PDF) direto na tela de Encaixe (§9.3, porte do
/// fluxo de <c>encaixe.js</c>) — substitui a antiga seleção por Molde salvo: o arquivo já
/// chega pronto pra encaixar, sem precisar cadastrar um molde antes.
/// </summary>
public partial class PecaDeArquivoEncaixe : ObservableObject
{
    public required string Id { get; init; }
    public required string Nome { get; init; }
    public required string NomeDoArquivo { get; init; }
    public required double Largura { get; init; }
    public required double Altura { get; init; }
    public required IReadOnlyList<PontoXY> Contorno { get; init; }
    public required string Origem { get; init; }

    /// <summary>A quantidade veio do próprio nome do arquivo ("frente 5x.png") — mostrado como dica na linha.</summary>
    public required bool QuantidadeVeioDoNome { get; init; }

    [ObservableProperty]
    public partial int Quantidade { get; set; } = 1;

    /// <summary>
    /// Giro permitido desta peça na busca (§11.11/§9.3) — <c>null</c> significa "usa o giro
    /// padrão" (segue ao vivo o seletor "Giro de todas as peças", em vez de congelar uma cópia
    /// no momento em que a peça foi adicionada — mudar o padrão depois de adicionar arquivos
    /// agora afeta toda peça que não teve exceção marcada). Só vira um valor explícito quando o
    /// usuário escolhe algo diferente de "(padrão)" na linha dessa peça.
    /// </summary>
    [ObservableProperty]
    public partial TipoDeGiro? Giro { get; set; }
}
