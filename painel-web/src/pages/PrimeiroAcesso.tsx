import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { bootstrapPrimeiroUsuario, ErroDaApi } from "../api/central";

/**
 * "Primeiro acesso" — cria o administrador inicial de uma instalação que acabou de provisionar
 * na Central e ainda não tem usuário nenhum (§24.7, bootstrap). Fecha sozinho depois do
 * primeiro cadastro bem-sucedido: uma segunda tentativa com o mesmo código volta 409, e o link
 * daqui em diante deixa de servir pra essa instalação.
 */
export function PrimeiroAcesso() {
  const navegar = useNavigate();

  const [codigo, setCodigo] = useState("");
  const [nome, setNome] = useState("");
  const [login, setLogin] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [criado, setCriado] = useState(false);

  const aoEnviar = async (evento: FormEvent) => {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      await bootstrapPrimeiroUsuario(codigo.trim().toUpperCase(), login.trim(), nome.trim(), senha);
      setCriado(true);
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : "Não foi possível criar o usuário. Confira a conexão.");
    } finally {
      setEnviando(false);
    }
  };

  if (criado) {
    return (
      <div className="tela-login">
        <div className="cartao-login">
          <h1>Optimize Pro</h1>
          <p className="apoio">Usuário criado — já pode entrar com o login e a senha que você acabou de definir.</p>
          <button type="button" onClick={() => navegar("/login", { replace: true })}>
            Ir para o login
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="tela-login">
      <form className="cartao-login" onSubmit={aoEnviar}>
        <h1>Optimize Pro</h1>
        <p className="apoio">Primeiro acesso — cria o administrador desta instalação</p>

        <label>
          Código da instalação
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
          Seu nome
          <input value={nome} onChange={(e) => setNome(e.target.value)} required />
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
          {enviando ? "Criando..." : "Criar acesso"}
        </button>

        <p className="apoio">
          Já tem usuário cadastrado? <Link to="/login">Entrar</Link>
        </p>
      </form>
    </div>
  );
}
