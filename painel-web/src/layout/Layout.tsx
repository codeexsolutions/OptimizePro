import { NavLink, Outlet } from "react-router-dom";
import { useAutenticacao } from "../auth/AuthContext";

const ROTAS_DOS_MODULOS: Record<string, { caminho: string; rotulo: string }> = {
  Impressoras: { caminho: "/impressoras", rotulo: "Impressoras" },
  Maquinas: { caminho: "/maquinas", rotulo: "Máquinas" },
  Historico: { caminho: "/historico", rotulo: "Histórico" },
  Reposicao: { caminho: "/reposicao", rotulo: "Reposição" },
  Pedidos: { caminho: "/pedidos", rotulo: "Pedidos" },
  OrdensDeServico: { caminho: "/ordens-servico", rotulo: "Ordens de Serviço" },
};

export function Layout() {
  const { sessao, sair } = useAutenticacao();
  if (!sessao) return null;

  return (
    <div className="casca">
      <aside className="barra-lateral">
        <div className="marca">
          <strong>Optimize</strong>
          <span className="apoio">Painel do proprietário</span>
        </div>

        <nav>
          {sessao.usuario.modulosLiberados.map((modulo) => {
            const rota = ROTAS_DOS_MODULOS[modulo];
            if (!rota) return null;
            return (
              <NavLink key={modulo} to={rota.caminho} className={({ isActive }) => (isActive ? "ativo" : "")}>
                {rota.rotulo}
              </NavLink>
            );
          })}
          {sessao.usuario.ehAdministrador && (
            <>
              <div className="separador" />
              <NavLink to="/usuarios" className={({ isActive }) => (isActive ? "ativo" : "")}>
                Usuários
              </NavLink>
              <NavLink to="/faturamento" className={({ isActive }) => (isActive ? "ativo" : "")}>
                Faturamento
              </NavLink>
            </>
          )}
        </nav>

        <div className="rodape-da-barra">
          <span>{sessao.usuario.nome}</span>
          <button onClick={sair}>Sair</button>
        </div>
      </aside>

      <main className="conteudo">
        <Outlet />
      </main>
    </div>
  );
}
