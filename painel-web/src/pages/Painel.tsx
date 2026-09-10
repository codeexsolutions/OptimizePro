import { useAutenticacao } from "../auth/AuthContext";

// Nomes que o app desktop usa nos módulos (ModuloDoPainel, OptimizePro.Painel) — rótulo em
// português pra tela; as telas de verdade de cada um chegam na fase 6.
const RETULO_DO_MODULO: Record<string, string> = {
  Impressoras: "Impressoras",
  Maquinas: "Máquinas",
  Historico: "Histórico",
  Reposicao: "Reposição",
  Pedidos: "Pedidos",
  OrdensDeServico: "Ordens de Serviço",
};

export function Painel() {
  const { sessao, sair } = useAutenticacao();
  if (!sessao) return null;

  return (
    <div className="painel">
      <header className="cabecalho-do-painel">
        <div>
          <strong>Optimize</strong>
          <span className="apoio">Painel do proprietário</span>
        </div>
        <div className="usuario-logado">
          <span>{sessao.usuario.nome}</span>
          <button onClick={sair}>Sair</button>
        </div>
      </header>

      <main>
        <h2>Módulos liberados</h2>
        {sessao.usuario.modulosLiberados.length === 0 && !sessao.usuario.ehAdministrador && (
          <p className="apoio">Nenhum módulo liberado ainda — fale com quem administra este painel.</p>
        )}
        <ul className="lista-de-modulos">
          {sessao.usuario.modulosLiberados.map((modulo) => (
            <li key={modulo}>{RETULO_DO_MODULO[modulo] ?? modulo}</li>
          ))}
          {sessao.usuario.ehAdministrador && (
            <>
              <li>Usuários</li>
              <li>Faturamento</li>
            </>
          )}
        </ul>
        <p className="apoio">
          As telas de cada módulo (dashboard, listas, indicadores) entram na próxima fase — por
          enquanto isto só confirma que o login e a sessão funcionam de ponta a ponta.
        </p>
      </main>
    </div>
  );
}
