import { Navigate, Route, BrowserRouter, Routes } from "react-router-dom";
import { ProvedorDeAutenticacao, useAutenticacao } from "./auth/AuthContext";
import { Login } from "./pages/Login";
import { Painel } from "./pages/Painel";
import "./App.css";

function RotaProtegida({ children }: { children: React.ReactNode }) {
  const { sessao, carregando } = useAutenticacao();

  if (carregando) return <div className="carregando">Carregando...</div>;
  if (!sessao) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function Rotas() {
  const { sessao, carregando } = useAutenticacao();

  return (
    <Routes>
      <Route path="/login" element={carregando ? null : sessao ? <Navigate to="/" replace /> : <Login />} />
      <Route
        path="/"
        element={
          <RotaProtegida>
            <Painel />
          </RotaProtegida>
        }
      />
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
