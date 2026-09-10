using OptimizePro.Core.Impressoras;

namespace OptimizePro.Services.Impressoras.Historico;

public sealed class FabricaDeLeitorDeHistorico(
    LeitorCsvHistorico csv, LeitorXmlHistorico xml, LeitorAtBinarioHistorico atBinario)
{
    public ILeitorDeHistorico ObterPara(TipoDeMaquina tipo) => tipo switch
    {
        TipoDeMaquina.Csv => csv,
        TipoDeMaquina.Xml => xml,
        TipoDeMaquina.AtBinario => atBinario,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };
}
