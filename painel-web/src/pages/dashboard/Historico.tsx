import { useDados } from "../../hooks/useDados";
import { obterHistorico } from "../../api/dashboard";
import { metros, dataHoraBr } from "../../format";

export function Historico() {
  const { dados, carregando, erro } = useDados(obterHistorico);

  return (
    <div className="pagina">
      <h1>Histórico</h1>
      <p className="apoio">Trabalhos que saíram das máquinas, últimos 90 dias.</p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}
      {dados && dados.length === 0 && <p className="apoio">Nenhum trabalho nesse período.</p>}

      {dados && dados.length > 0 && (
        <div className="tabela-scroll">
          <table>
            <thead>
              <tr>
                <th>Data</th>
                <th>Máquina</th>
                <th>Trabalho</th>
                <th>Metragem</th>
                <th>Situação</th>
              </tr>
            </thead>
            <tbody>
              {[...dados]
                .sort((a, b) => b.dataHora.localeCompare(a.dataHora))
                .map((registro) => (
                  <tr key={registro.id}>
                    <td className="mono">{dataHoraBr(registro.dataHora)}</td>
                    <td>{registro.nomeDaMaquina}</td>
                    <td className="celula-truncada" title={registro.tarefa ?? ""}>
                      {registro.tarefa}
                    </td>
                    <td className="mono">{metros(registro.comprimentoDeImpressao)}</td>
                    <td>{registro.cancelada ? "Cancelado" : registro.comErro ? "Erro" : registro.status ?? "Concluído"}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
