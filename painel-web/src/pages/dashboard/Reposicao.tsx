import { useState } from "react";
import { useDados } from "../../hooks/useDados";
import { obterReposicao } from "../../api/dashboard";
import { metros, dataBr, dataHoraBr } from "../../format";

export function Reposicao() {
  const { dados, carregando, erro } = useDados(obterReposicao);
  const [abertas, setAbertas] = useState<Set<string>>(new Set());

  const alternar = (semana: string) =>
    setAbertas((antes) => {
      const proximo = new Set(antes);
      if (proximo.has(semana)) proximo.delete(semana);
      else proximo.add(semana);
      return proximo;
    });

  return (
    <div className="pagina">
      <h1>Reposição</h1>
      <p className="apoio">
        Trabalho refeito, por semana. Reconhecido pela palavra "reposição" no nome do arquivo —
        é um piso, não um total.
      </p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}
      {dados && dados.quantidadeTotal === 0 && <p className="apoio">Nenhuma reposição no histórico.</p>}

      {dados && dados.quantidadeTotal > 0 && (
        <>
          <div className="destaque">
            <strong>{metros(dados.metragemTotal)}</strong>
            <span className="apoio">
              {dados.quantidadeTotal} reposição(ões) em {dados.semanas.length} semana(s)
            </span>
          </div>

          <ul className="lista-de-acordeao">
            {dados.semanas.map((semana) => {
              const aberta = abertas.has(semana.inicioDaSemana);
              return (
                <li key={semana.inicioDaSemana}>
                  <button className="linha-do-acordeao" onClick={() => alternar(semana.inicioDaSemana)}>
                    <span>{aberta ? "▾" : "▸"}</span>
                    <span className="flex-1">
                      {dataBr(semana.inicioDaSemana)} a {dataBr(semana.fimDaSemana)}
                    </span>
                    <span className="apoio">{semana.quantidade} trabalho(s)</span>
                    <strong>{metros(semana.metragemTotal)}</strong>
                  </button>
                  {aberta && (
                    <div className="detalhe-do-acordeao">
                      {semana.itens.map((item) => (
                        <div key={item.id} className="linha-do-detalhe">
                          <span className="mono apoio">{dataHoraBr(item.dataHora)}</span>
                          <span className="apoio">{item.nomeDaMaquina}</span>
                          <span className="celula-truncada">{item.tarefa}</span>
                          <span className="mono">{metros(item.comprimentoDeImpressao)}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        </>
      )}
    </div>
  );
}
