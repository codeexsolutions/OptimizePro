import { useDados } from "../../hooks/useDados";
import { obterFaturamento } from "../../api/faturamento";
import { reais, dataBr } from "../../format";

function statusDaLicenca(validaAte: string | null): { rotulo: string; classe: string } | null {
  if (!validaAte) return null;
  const hoje = new Date().toISOString().slice(0, 10);
  const venceEmBreve = validaAte <= new Date(Date.now() + 7 * 86_400_000).toISOString().slice(0, 10);
  if (validaAte < hoje) return { rotulo: "Vencida", classe: "selo-off" };
  if (venceEmBreve) return { rotulo: "Vence em breve", classe: "selo-off" };
  return { rotulo: "Em dia", classe: "selo-ok" };
}

export function Faturamento() {
  const { dados, carregando, erro } = useDados(obterFaturamento);

  return (
    <div className="pagina">
      <h1>Faturamento</h1>
      <p className="apoio">Mensalidade devida ao Optimize e validade da licença desta instalação.</p>

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}

      {!carregando && !erro && dados === null && (
        <p className="apoio">
          Ainda não há dados de faturamento sincronizados — o app desktop sincroniza a cada
          poucos minutos quando está online; se acabou de ativar a licença, aguarde o próximo ciclo.
        </p>
      )}

      {dados && (
        <div className="grade-de-cartoes">
          <div className="cartao">
            <div className="cartao-cabecalho">
              <span className="apoio">Mensalidade atual</span>
            </div>
            <div className="destaque">
              <strong>{reais(dados.mensalidadeTotal)}</strong>
            </div>
            <div className="ultimo-trabalho">
              <span className="apoio">Base do plano: {reais(dados.valorBaseMensal)}</span>
              <span className="apoio">
                {dados.usuariosHabilitados} de {dados.limiteDeUsuariosNoPlano} usuário(s) do plano padrão
              </span>
              {dados.usuariosExtras > 0 && (
                <span className="texto-alerta">
                  +{dados.usuariosExtras} usuário(s) extra(s) × {reais(dados.valorPorUsuarioExtra)} = {reais(dados.usuariosExtras * dados.valorPorUsuarioExtra)}
                </span>
              )}
            </div>
          </div>

          <div className="cartao">
            <div className="cartao-cabecalho">
              <span className="apoio">Licença</span>
              {statusDaLicenca(dados.licencaValidaAte) && (
                <span className={`selo ${statusDaLicenca(dados.licencaValidaAte)!.classe}`}>
                  {statusDaLicenca(dados.licencaValidaAte)!.rotulo}
                </span>
              )}
            </div>
            <div className="destaque">
              <strong>{dados.licencaValidaAte ? dataBr(dados.licencaValidaAte) : "—"}</strong>
            </div>
            <span className="apoio">{dados.licencaValidaAte ? "Vence em" : "Sem informação de validade sincronizada ainda"}</span>
          </div>
        </div>
      )}
    </div>
  );
}
