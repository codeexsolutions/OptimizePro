import { Navigate, Route, BrowserRouter, Routes } from "react-router-dom";
import { ProvedorDeAutenticacao, useAutenticacao } from "./auth/AuthContext";
import { Login } from "./pages/Login";
import { Layout } from "./layout/Layout";
import { Maquinas } from "./pages/dashboard/Maquinas";
import { Impressoras } from "./pages/dashboard/Impressoras";
import { Historico } from "./pages/dashboard/Historico";
import { Reposicao } from "./pages/dashboard/Reposicao";
import { Pedidos } from "./pages/dashboard/Pedidos";
import { OrdensDeServico } from "./pages/dashboard/OrdensDeServico";
import { Usuarios } from "./pages/admin/Usuarios";
import { Faturamento } from "./pages/admin/Faturamento";
import "./App.css";

function RotaProtegida({ children }: { children: React.ReactNode }) {
  const { sessao, carregando } = useAutenticacao();

  if (carregando) return <div className="carregando">Carregando...</div>;
  if (!sessao) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function RotaDeAdministrador({ children }: { children: React.ReactNode }) {
  const { sessao } = useAutenticacao();
  if (!sessao?.usuario.ehAdministrador) return <Navigate to="/" replace />;
  return <>{children}</>;
}

// Primeira tela liberada da pessoa — não força todo mundo pra "/maquinas" quando o módulo
// dela nem é esse; segue a mesma ordem que a barra lateral usa.
function primeiraRotaLiberada(modulos: string[]): string {
  const ordem: Record<string, string> = {
    Impressoras: "/impressoras",
    Maquinas: "/maquinas",
    Historico: "/historico",
    Reposicao: "/reposicao",
    Pedidos: "/pedidos",
    OrdensDeServico: "/ordens-servico",
  };
  for (const chave of Object.keys(ordem)) {
    if (modulos.includes(chave)) return ordem[chave];
  }
  return "/sem-acesso";
}

function Rotas() {
  const { sessao, carregando } = useAutenticacao();

  return (
    <Routes>
      <Route path="/login" element={carregando ? null : sessao ? <Navigate to="/" replace /> : <Login />} />
      <Route
        element={
          <RotaProtegida>
            <Layout />
          </RotaProtegida>
        }
      >
        <Route path="/" element={<Navigate to={sessao ? primeiraRotaLiberada(sessao.usuario.modulosLiberados) : "/login"} replace />} />
        <Route path="/impressoras" element={<Impressoras />} />
        <Route path="/maquinas" element={<Maquinas />} />
        <Route path="/historico" element={<Historico />} />
        <Route path="/reposicao" element={<Reposicao />} />
        <Route path="/pedidos" element={<Pedidos />} />
        <Route path="/ordens-servico" element={<OrdensDeServico />} />
        <Route
          path="/usuarios"
          element={
            <RotaDeAdministrador>
              <Usuarios />
            </RotaDeAdministrador>
          }
        />
        <Route
          path="/faturamento"
          element={
            <RotaDeAdministrador>
              <Faturamento />
            </RotaDeAdministrador>
          }
        />
        <Route
          path="/sem-acesso"
          element={
            <div className="pagina">
              <h1>Nenhum módulo liberado</h1>
              <p className="apoio">Fale com quem administra este painel pra liberar o acesso.</p>
            </div>
          }
        />
      </Route>
    </Routes>
  );
}

function App() {
  return (
    <ProvedorDeAutenticacao>
      <BrowserRouter>
        <Rotas />
      </BrowserRouter>
    </ProvedorDeAutenticacao>
  );
}

export default App;
