using Microsoft.AspNetCore.SignalR;

namespace OptimizePro.Servidor;

/// <summary>
/// Push em tempo real pro painel da frota de impressoras — o equivalente ao socket.io do
/// optmize-full (§22.2). Existe porque o usuário confirmou que várias pessoas olham o painel
/// ao mesmo tempo, então a tela não pode depender de polling do próprio cliente.
///
/// Por enquanto é só o esqueleto: os métodos de notificação (máquina atualizada, trabalho
/// novo, progresso de impressão) entram junto com o serviço de varredura/polling nas fases
/// seguintes, que vão chamar <see cref="IHubContext{PainelHub}"/> pra emitir os eventos.
/// </summary>
public class PainelHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("conectado", new { ok = true });
        await base.OnConnectedAsync();
    }
}
