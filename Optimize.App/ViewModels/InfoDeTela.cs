using System.Collections.Generic;
using FluentAvalonia.UI.Controls;

namespace Optimize.App.ViewModels;

/// <summary>
/// Porte da tabela `TELAS` do optmize-full (<c>src/rotas.ts</c>) — "uma tela é uma linha
/// aqui e mais nada": o menu lateral e o cabeçalho saem todos dela. Cobre também as
/// sub-telas (assistente de molde, editor de projeto) que não aparecem no menu, mas ainda
/// precisam de título/ícone pro cabeçalho quando abertas.
/// </summary>
public sealed record InfoDeTela(string Titulo, string ApoioMenu, string ApoioTopo, Symbol Icone);

public static class InfoDasTelas
{
    public static readonly IReadOnlyDictionary<TipoDeTela, InfoDeTela> Todas = new Dictionary<TipoDeTela, InfoDeTela>
    {
        [TipoDeTela.Moldes] = new("Moldes", "Modelagem da produção", "Centralize moldes, tamanhos e estampas da produção.", Symbol.Cut),
        [TipoDeTela.MoldeWizard] = new("Novo molde", "Passo a passo", "Monte um molde novo, parte por parte.", Symbol.Cut),
        [TipoDeTela.Projetos] = new("Projetos", "Trabalho que se repete", "Guarde por cliente o trabalho pronto para repetir e mandar ao encaixe.", Symbol.Folder),
        [TipoDeTela.ProjetoEditor] = new("Projeto", "Edição do projeto", "Ajuste as peças e mande para o encaixe.", Symbol.Folder),
        [TipoDeTela.Encaixe] = new("Encaixe", "Aproveitamento do tecido", "Otimize o uso do tecido e prepare arquivos para impressão.", Symbol.ViewAll),
        [TipoDeTela.Vetor] = new("Vetor", "Traço a partir da imagem", "Transforme uma imagem em desenho vetorial para corte e impressão.", Symbol.ImageEdit),
        [TipoDeTela.Disparo] = new("Disparo", "Captação de lead (em stand-by)", "Envio de mensagens — recurso em pausa.", Symbol.Send),
        [TipoDeTela.Configuracoes] = new("Configurações", "Preferências do app", "Ajustes gerais do Optimize.", Symbol.Settings),
    };
}
