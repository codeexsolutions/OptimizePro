import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { login as loginNaApi, obterUsuarioAtual, type UsuarioLogado } from "../api/central";

const CHAVE_DE_SESSAO = "optimize-painel:sessao";

interface SessaoGuardada {
  token: string;
  usuario: UsuarioLogado;
}

interface ContextoDeAutenticacao {
  sessao: SessaoGuardada | null;
  carregando: boolean;
  entrar: (codigo: string, login: string, senha: string) => Promise<void>;
  sair: () => void;
}

const ContextoDeAutenticacaoBase = createContext<ContextoDeAutenticacao | null>(null);

function lerSessaoGuardada(): SessaoGuardada | null {
  try {
    const bruto = localStorage.getItem(CHAVE_DE_SESSAO);
    return bruto ? (JSON.parse(bruto) as SessaoGuardada) : null;
  } catch {
    return null; // storage bloqueado/corrompido — trata como "nunca logou", não trava a tela.
  }
}

export function ProvedorDeAutenticacao({ children }: { children: ReactNode }) {
  const [sessao, setSessao] = useState<SessaoGuardada | null>(null);
  const [carregando, setCarregando] = useState(true);

  // Ao carregar a página, confere se o token guardado ainda vale (não expirou/foi revogado)
  // antes de considerar a pessoa "logada" — sem isso, um token vencido pareceria uma sessão
  // válida até a primeira chamada de verdade falhar.
  useEffect(() => {
    const guardada = lerSessaoGuardada();
    if (!guardada) {
      setCarregando(false);
      return;
    }

    obterUsuarioAtual(guardada.token)
      .then((usuario) => setSessao({ token: guardada.token, usuario }))
      .catch(() => {
        localStorage.removeItem(CHAVE_DE_SESSAO);
        setSessao(null);
      })
      .finally(() => setCarregando(false));
  }, []);

  const entrar = async (codigo: string, loginDigitado: string, senha: string) => {
    const resposta = await loginNaApi(codigo, loginDigitado, senha);
    const nova: SessaoGuardada = { token: resposta.token, usuario: resposta.usuario };
    setSessao(nova);
    try {
      localStorage.setItem(CHAVE_DE_SESSAO, JSON.stringify(nova));
    } catch {
      // Sem storage disponível (janela anônima, etc.) — a sessão ainda funciona nesta aba,
      // só não sobrevive a um F5.
    }
  };

  const sair = () => {
    setSessao(null);
    localStorage.removeItem(CHAVE_DE_SESSAO);
  };

  return (
    <ContextoDeAutenticacaoBase.Provider value={{ sessao, carregando, entrar, sair }}>
      {children}
    </ContextoDeAutenticacaoBase.Provider>
  );
}

export function useAutenticacao(): ContextoDeAutenticacao {
  const contexto = useContext(ContextoDeAutenticacaoBase);
  if (!contexto) throw new Error("useAutenticacao só pode ser usado dentro de ProvedorDeAutenticacao.");
  return contexto;
}
