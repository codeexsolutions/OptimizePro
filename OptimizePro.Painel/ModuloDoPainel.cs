namespace OptimizePro.Painel;

/// <summary>
/// Um módulo do painel do proprietário (§23) — os mesmos seis domínios do chão de fábrica que
/// já existem no app desktop (Impressoras/Máquinas/Histórico/Reposição/Pedidos/Ordens de
/// Serviço), mais o de Disparo. "Usuários" e "Faturamento" não entram aqui: são exclusivos de
/// quem tem <see cref="Usuario.EhAdministrador"/>, não algo que se libera por usuário.
/// </summary>
public enum ModuloDoPainel
{
    Impressoras,
    Maquinas,
    Historico,
    Reposicao,
    Pedidos,
    OrdensDeServico,
}
