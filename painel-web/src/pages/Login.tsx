import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAutenticacao } from "../auth/AuthContext";
import { ErroDaApi } from "../api/central";

export function Login() {
  const { entrar } = useAutenticacao();
  const navegar = useNavigate();

  const [codigo, setCodigo] = useState("");
  const [login, setLogin] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  const aoEnviar = async (evento: FormEvent) => {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      await entrar(codigo.trim().toUpperCase(), login.trim(), senha);
      navegar("/", { replace: true });
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : "Não foi possível entrar. Confira a conexão.");
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="tela-login">
      <form className="cartao-login" onSubmit={aoEnviar}>
        <h1>Optimize</h1>
        <p className="apoio">Painel do proprietário</p>

        <label>
          Empresa
          <input
            value={codigo}
            onChange={(e) => setCodigo(e.target.value)}
            placeholder="Código de 6 letras"
            maxLength={6}
            autoCapitalize="characters"
            required
          />
        </label>

        <label>
          Login
          <input value={login} onChange={(e) => setLogin(e.target.value)} required />
        </label>

        <label>
          Senha
          <input type="password" value={senha} onChange={(e) => setSenha(e.target.value)} required />
        </label>

        {erro && <p className="erro">{erro}</p>}

        <button type="submit" disabled={enviando}>
          {enviando ? "Entrando..." : "Entrar"}
        </button>

        <p className="apoio">
          Instalação nova, sem usuário ainda? <Link to="/primeiro-acesso">Criar o primeiro acesso</Link>
        </p>
      </form>
    </div>
  );
}
