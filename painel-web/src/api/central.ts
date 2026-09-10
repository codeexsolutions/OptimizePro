// Cliente HTTP da Central (§24.5) — sem lib externa, fetch simples chega.
const URL_BASE = import.meta.env.VITE_CENTRAL_URL as string;

export interface UsuarioLogado {
  usuarioId: string;
  login: string;
  nome: string;
  ehAdministrador: boolean;
  modulosLiberados: string[];
}

export interface RespostaDeLogin {
  token: string;
  usuario: UsuarioLogado;
}

export class ErroDaApi extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

export async function login(codigo: string, login: string, senha: string): Promise<RespostaDeLogin> {
  const resposta = await fetch(`${URL_BASE}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ codigo, login, senha }),
  });

  if (!resposta.ok) {
    const corpo = await resposta.json().catch(() => null);
    throw new ErroDaApi(corpo?.erro ?? "Não foi possível entrar.", resposta.status);
  }

  return resposta.json();
}

export async function obterUsuarioAtual(token: string): Promise<UsuarioLogado> {
  const resposta = await fetch(`${URL_BASE}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!resposta.ok) throw new ErroDaApi("Sessão expirada.", resposta.status);

  const dados = await resposta.json();
  return {
    usuarioId: dados.usuarioId,
    login: dados.login ?? "",
    nome: dados.nome,
    ehAdministrador: dados.ehAdministrador,
    modulosLiberados: dados.modulosLiberados,
  };
}
