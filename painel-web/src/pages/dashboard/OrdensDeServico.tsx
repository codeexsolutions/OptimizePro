import { useDados } from "../../hooks/useDados";
import { obterOrdensDeServico } from "../../api/dashboard";
import { metros, dataBr } from "../../format";

export function OrdensDeServico() {
  const { dados, carregando, erro } = useDados(obterOrdensDeServico);

  return (
    <div className="pagina">
      <h1>Ordens de Serviço</h1>
      <p className="apoio">Referência de imagem por cliente — só leitura aqui.</p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}
      {dados && dados.length === 0 && <p className="apoio">Nenhuma ordem de serviço ainda.</p>}

      {dados && dados.length > 0 && (
        <div className="tabela-scroll">
          <table>
            <thead>
              <tr>
                <th>Cliente</th>
                <th>Tecido</th>
                <th>Data</th>
                <th>Metros</th>
                <th>Imagens</th>
              </tr>
            </thead>
            <tbody>
              {[...dados]
                .sort((a, b) => b.data.localeCompare(a.data))
                .map((ordem) => (
                  <tr key={ordem.id}>
                    <td>{ordem.nomeDoCliente}</td>
                    <td>{ordem.tecido}</td>
                    <td className="mono">{dataBr(ordem.data)}</td>
                    <td className="mono">{ordem.metros != null ? metros(ordem.metros) : "—"}</td>
                    <td>{ordem.quantidadeDeImagens}</td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
