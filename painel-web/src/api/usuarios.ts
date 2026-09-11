import { ErroDaApi } from "./central";

const URL_BASE = import.meta.env.VITE_CENTRAL_URL as string;

export interface UsuarioDoPainel {
  id: string;
  login: string;
  nome: string;
  ehAdministrador: boolean;
  habilitado: boolean;
  modulosLiberados: string[];
}

export interface NovoUsuario {
  login: string;
  nome: string;
  senha: string;
  modulosLiberados: string[];
  ehAdministrador: boolean;
}

export interface EdicaoDeUsuario {
  nome: string;
  modulosLiberados: string[];
  ehAdministrador: boolean;
}

async function chamar<T>(caminho: string, token: string, opcoes: RequestInit = {}): Promise<T> {
  const resposta = await fetch(`${URL_BASE}${caminho}`, {
    ...opcoes,
    headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json", ...opcoes.headers },
  });

  if (!resposta.ok) {
    const corpo = await resposta.json().catch(() => null);
    throw new ErroDaApi(corpo?.erro ?? "Não foi possível completar a operação.", resposta.status);
  }

  if (resposta.status === 204 || resposta.headers.get("content-length") === "0") return undefined as T;
  return resposta.json().catch(() => undefined as T);
}

export const listarUsuarios = (token: string) => chamar<UsuarioDoPainel[]>("/api/usuarios", token);

export const cadastrarUsuario = (token: string, dados: NovoUsuario) =>
  chamar<UsuarioDoPainel>("/api/usuarios", token, {
    method: "POST",
    body: JSON.stringify({ Login: dados.login, Nome: dados.nome, Senha: dados.senha, ModulosLiberados: dados.modulosLiberados, EhAdministrador: dados.ehAdministrador }),
  });

export const atualizarUsuario = (token: string, id: string, dados: EdicaoDeUsuario) =>
  chamar<void>(`/api/usuarios/${id}`, token, {
    method: "PUT",
    body: JSON.stringify({ Nome: dados.nome, ModulosLiberados: dados.modulosLiberados, EhAdministrador: dados.ehAdministrador }),
  });

export const redefinirSenhaDoUsuario = (token: string, id: string, novaSenha: string) =>
  chamar<void>(`/api/usuarios/${id}/redefinir-senha`, token, {
    method: "POST",
    body: JSON.stringify({ NovaSenha: novaSenha }),
  });

export const atualizarHabilitadoDoUsuario = (token: string, id: string, habilitado: boolean) =>
  chamar<void>(`/api/usuarios/${id}/habilitado`, token, {
    method: "PUT",
    body: JSON.stringify({ Habilitado: habilitado }),
  });

export const excluirUsuario = (token: string, id: string) =>
  chamar<void>(`/api/usuarios/${id}`, token, { method: "DELETE" });
