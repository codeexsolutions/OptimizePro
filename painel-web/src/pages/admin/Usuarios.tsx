import { useState } from "react";
import { useAutenticacao } from "../../auth/AuthContext";
import { useDados } from "../../hooks/useDados";
import {
  listarUsuarios,
  cadastrarUsuario,
  atualizarUsuario,
  redefinirSenhaDoUsuario,
  atualizarHabilitadoDoUsuario,
  excluirUsuario,
  type UsuarioDoPainel,
} from "../../api/usuarios";
import { ErroDaApi } from "../../api/central";

const MODULOS: { chave: string; rotulo: string }[] = [
  { chave: "Impressoras", rotulo: "Impressoras" },
  { chave: "Maquinas", rotulo: "Máquinas" },
  { chave: "Historico", rotulo: "Histórico" },
  { chave: "Reposicao", rotulo: "Reposição" },
  { chave: "Pedidos", rotulo: "Pedidos" },
  { chave: "OrdensDeServico", rotulo: "Ordens de Serviço" },
];

function ToggleDeModulos({ selecionados, onMudar }: { selecionados: string[]; onMudar: (modulos: string[]) => void }) {
  const alternar = (chave: string) => {
    onMudar(selecionados.includes(chave) ? selecionados.filter((m) => m !== chave) : [...selecionados, chave]);
  };

  return (
    <div className="grade-de-modulos">
      {MODULOS.map((m) => (
        <label key={m.chave} className="modulo-checkbox">
          <input type="checkbox" checked={selecionados.includes(m.chave)} onChange={() => alternar(m.chave)} />
          {m.rotulo}
        </label>
      ))}
    </div>
  );
}

function FormularioDeNovoUsuario({ onCriado }: { onCriado: () => void }) {
  const { sessao } = useAutenticacao();
  const [login, setLogin] = useState("");
  const [nome, setNome] = useState("");
  const [senha, setSenha] = useState("");
  const [ehAdministrador, setEhAdministrador] = useState(false);
  const [modulos, setModulos] = useState<string[]>([]);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const enviar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!sessao) return;
    setEnviando(true);
    setErro(null);
    try {
      await cadastrarUsuario(sessao.token, { login, nome, senha, modulosLiberados: modulos, ehAdministrador });
      setLogin("");
      setNome("");
      setSenha("");
      setEhAdministrador(false);
      setModulos([]);
      onCriado();
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : "Não foi possível cadastrar o usuário.");
    } finally {
      setEnviando(false);
    }
  };

  return (
    <form className="cartao formulario-de-usuario" onSubmit={enviar}>
      <h2>Novo usuário</h2>
      <div className="linha-do-formulario">
        <label>
          Login
          <input value={login} onChange={(e) => setLogin(e.target.value)} required />
        </label>
        <label>
          Nome
          <input value={nome} onChange={(e) => setNome(e.target.value)} required />
        </label>
        <label>
          Senha
          <input type="password" value={senha} onChange={(e) => setSenha(e.target.value)} required minLength={6} />
        </label>
      </div>

      <label className="apoio">Módulos liberados</label>
      <ToggleDeModulos selecionados={modulos} onMudar={setModulos} />

      <label className="checkbox-administrador">
        <input type="checkbox" checked={ehAdministrador} onChange={(e) => setEhAdministrador(e.target.checked)} />
        Administrador (também vê Usuários e Faturamento)
      </label>

      {erro && <p className="erro">{erro}</p>}
      <button type="submit" disabled={enviando}>
        {enviando ? "Cadastrando..." : "Cadastrar usuário"}
      </button>
    </form>
  );
}

function LinhaDeUsuario({ usuario, souEu, onMudou }: { usuario: UsuarioDoPainel; souEu: boolean; onMudou: () => void }) {
  const { sessao } = useAutenticacao();
  const [editando, setEditando] = useState(false);
  const [nome, setNome] = useState(usuario.nome);
  const [ehAdministrador, setEhAdministrador] = useState(usuario.ehAdministrador);
  const [modulos, setModulos] = useState<string[]>(usuario.modulosLiberados);
  const [novaSenha, setNovaSenha] = useState("");
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const salvar = async () => {
    if (!sessao) return;
    setSalvando(true);
    setErro(null);
    try {
      await atualizarUsuario(sessao.token, usuario.id, { nome, modulosLiberados: modulos, ehAdministrador });
      if (novaSenha.trim()) {
        await redefinirSenhaDoUsuario(sessao.token, usuario.id, novaSenha.trim());
      }
      setNovaSenha("");
      setEditando(false);
      onMudou();
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : "Não foi possível salvar.");
    } finally {
      setSalvando(false);
    }
  };

  const alternarHabilitado = async () => {
    if (!sessao) return;
    setErro(null);
    try {
      await atualizarHabilitadoDoUsuario(sessao.token, usuario.id, !usuario.habilitado);
      onMudou();
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : "Não foi possível alterar.");
    }
  };

  const excluir = async () => {
    if (!sessao) return;
    if (!confirm(`Excluir o usuário "${usuario.login}"? Essa ação não pode ser desfeita.`)) return;
    setErro(null);
    try {
      await excluirUsuario(sessao.token, usuario.id);
      onMudou();
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : "Não foi possível excluir.");
    }
  };

  if (!editando) {
    return (
      <li>
        <div className="linha-do-acordeao" style={{ cursor: "default" }}>
          <span className="flex-1">
            <strong>{usuario.nome}</strong> <span className="apoio">@{usuario.login}</span>
            {souEu && <span className="selo selo-ok" style={{ marginLeft: 8 }}>você</span>}
          </span>
          <span className="apoio">
            {usuario.ehAdministrador ? "Administrador" : `${usuario.modulosLiberados.length} módulo(s)`}
          </span>
          <span className={`selo ${usuario.habilitado ? "selo-ok" : "selo-off"}`}>
            {usuario.habilitado ? "Ativo" : "Desativado"}
          </span>
          <button onClick={() => setEditando(true)}>Editar</button>
          <button onClick={alternarHabilitado} disabled={souEu}>
            {usuario.habilitado ? "Desativar" : "Ativar"}
          </button>
          <button onClick={excluir} disabled={souEu}>
            Excluir
          </button>
        </div>
        {erro && <p className="erro" style={{ padding: "0 14px 10px" }}>{erro}</p>}
      </li>
    );
  }

  return (
    <li>
      <div className="detalhe-do-acordeao">
        <div className="linha-do-formulario">
          <label>
            Nome
            <input value={nome} onChange={(e) => setNome(e.target.value)} />
          </label>
          <label>
            Redefinir senha (opcional)
            <input type="password" placeholder="deixe em branco pra manter" value={novaSenha} onChange={(e) => setNovaSenha(e.target.value)} />
          </label>
        </div>

        <label className="apoio">Módulos liberados</label>
        <ToggleDeModulos selecionados={modulos} onMudar={setModulos} />

        <label className="checkbox-administrador">
          <input
            type="checkbox"
            checked={ehAdministrador}
            disabled={souEu && ehAdministrador}
            onChange={(e) => setEhAdministrador(e.target.checked)}
          />
          Administrador
        </label>

        {erro && <p className="erro">{erro}</p>}
        <div>
          <button onClick={salvar} disabled={salvando}>
            {salvando ? "Salvando..." : "Salvar"}
          </button>
          <button onClick={() => setEditando(false)} disabled={salvando}>
            Cancelar
          </button>
        </div>
      </div>
    </li>
  );
}

export function Usuarios() {
  const { sessao } = useAutenticacao();
  const [versao, setVersao] = useState(0);
  const { dados, carregando, erro } = useDados(listarUsuarios, [versao]);
  const recarregar = () => setVersao((v) => v + 1);

  return (
    <div className="pagina">
      <h1>Usuários</h1>
      <p className="apoio">
        Plano padrão inclui até 7 usuários habilitados; acima disso a mensalidade cresce (ver Faturamento).
      </p>

      <FormularioDeNovoUsuario onCriado={recarregar} />

      {carregando && <p className="apoio">Carregando...</p>}
      {erro && <p className="erro">{erro}</p>}

      {dados && (
        <>
          <p className="apoio" style={{ marginTop: 20 }}>
            {dados.filter((u) => u.habilitado).length} usuário(s) habilitado(s) de {dados.length} cadastrado(s).
          </p>
          <ul className="lista-de-acordeao">
            {dados.map((u) => (
              <LinhaDeUsuario key={u.id} usuario={u} souEu={u.id === sessao?.usuario.usuarioId} onMudou={recarregar} />
            ))}
          </ul>
        </>
      )}
    </div>
  );
}
