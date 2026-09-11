import { useDados } from "../../hooks/useDados";
import { obterImpressoras } from "../../api/dashboard";
import { metros, dataHoraBr } from "../../format";

export function Impressoras() {
  const { dados, carregando, erro } = useDados(obterImpressoras);

  return (
    <div className="pagina">
      <h1>Impressoras</h1>
      <p className="apoio">
        Trabalhos e metragem de hoje, por máquina. Sincronizado periodicamente — não é ao vivo
        como o painel local da fábrica.
      </p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}
      {dados && dados.length === 0 && <p className="apoio">Nenhuma máquina cadastrada ainda.</p>}

      {dados && dados.length > 0 && (
        <div className="grade-de-cartoes">
          {dados.map((impressora) => (
            <div key={impressora.maquinaId} className="cartao">
              <div className="cartao-cabecalho">
                <strong>{impressora.nome}</strong>
                <span className={`selo ${impressora.habilitada ? "selo-ok" : "selo-off"}`}>
                  {impressora.habilitada ? "Habilitada" : "Desabilitada"}
                </span>
              </div>
              <div className="par-de-indicadores">
                <div>
                  <span className="apoio">Trabalhos hoje</span>
                  <strong>{impressora.trabalhosHoje}</strong>
                </div>
                <div>
                  <span className="apoio">Metragem hoje</span>
                  <strong>{metros(impressora.metragemHoje)}</strong>
                </div>
              </div>
              {impressora.ultimoTrabalho && (
                <div className="ultimo-trabalho">
                  <span className="apoio">Último trabalho</span>
                  <span>{impressora.ultimoTrabalho}</span>
                  <span className="apoio">{dataHoraBr(impressora.ultimoHorario)}</span>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
