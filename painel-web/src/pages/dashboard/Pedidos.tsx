import { useState } from "react";
import { useDados } from "../../hooks/useDados";
import { obterPedidos } from "../../api/dashboard";
import { metros, dataBr, dataHoraBr } from "../../format";

const ROTULO_DO_ANDAMENTO: Record<string, string> = {
  aberto: "Em aberto",
  pausado: "Pausado",
  concluido: "Concluído",
};

export function Pedidos() {
  const { dados, carregando, erro } = useDados(obterPedidos);
  const [aberto, setAberto] = useState<string | null>(null);

  return (
    <div className="pagina">
      <h1>Pedidos</h1>
      <p className="apoio">A fila da calandra — só leitura aqui; quem fecha o ciclo é a calandra.</p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}
      {dados && dados.length === 0 && <p className="apoio">Nenhum pedido ainda.</p>}

      {dados && dados.length > 0 && (
        <ul className="lista-de-acordeao">
          {[...dados]
            .sort((a, b) => b.criadoEm.localeCompare(a.criadoEm))
            .map((pedido) => {
              const ok = pedido.itens.filter((i) => i.statusNaCalandra === "ok").length;
              const comErro = pedido.itens.filter((i) => i.statusNaCalandra === "erro").length;
              const estaAberto = aberto === pedido.id;
              return (
                <li key={pedido.id}>
                  <button className="linha-do-acordeao" onClick={() => setAberto(estaAberto ? null : pedido.id)}>
                    <span>{estaAberto ? "▾" : "▸"}</span>
                    <span className="flex-1">{dataHoraBr(pedido.criadoEm.replace("T", " "))}</span>
                    <span className="apoio">{ROTULO_DO_ANDAMENTO[pedido.status] ?? pedido.status}</span>
                    <span className="mono apoio">
                      {ok}/{pedido.itens.length}
                      {comErro > 0 && <span className="texto-alerta"> {comErro} erro</span>}
                    </span>
                  </button>
                  {estaAberto && (
                    <div className="detalhe-do-acordeao">
                      {pedido.itens.map((item) => (
                        <div key={item.id} className="linha-do-detalhe">
                          <span className="mono apoio">{item.posicao + 1}</span>
                          <span className="celula-truncada">{item.tarefa}</span>
                          <span className="apoio">
                            {item.nomeDoCliente} — {item.nomeDaMaquina} · {dataBr(item.data)}
                          </span>
                          <span className="mono">{metros(item.comprimentoDeImpressao)}</span>
                          <span className="apoio">{item.statusNaCalandra}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </li>
              );
            })}
        </ul>
      )}
    </div>
  );
}
