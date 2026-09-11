import { useDados } from "../../hooks/useDados";
import { obterMaquinas } from "../../api/dashboard";

export function Maquinas() {
  const { dados, carregando, erro } = useDados(obterMaquinas);

  return (
    <div className="pagina">
      <h1>Máquinas</h1>
      <p className="apoio">Frota de impressoras cadastrada nesta fábrica.</p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}
      {dados && dados.length === 0 && <p className="apoio">Nenhuma máquina cadastrada ainda.</p>}

      {dados && dados.length > 0 && (
        <div className="grade-de-cartoes">
          {dados.map((maquina) => (
            <div key={maquina.id} className="cartao">
              <div className="cartao-cabecalho">
                <strong>{maquina.nome}</strong>
                <span className={`selo ${maquina.habilitada ? "selo-ok" : "selo-off"}`}>
                  {maquina.habilitada ? "Habilitada" : "Desabilitada"}
                </span>
              </div>
              <span className="apoio">{maquina.tipo}</span>
              {maquina.host && <span className="apoio">Host: {maquina.host}</span>}
              {maquina.ip && <span className="apoio">IP: {maquina.ip}</span>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
