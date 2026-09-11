import { ErroDaApi } from "./central";

const URL_BASE = import.meta.env.VITE_CENTRAL_URL as string;

export interface ResumoDeFaturamento {
  valorBaseMensal: number;
  valorPorUsuarioExtra: number;
  limiteDeUsuariosNoPlano: number;
  usuariosHabilitados: number;
  usuariosExtras: number;
  mensalidadeTotal: number;
  licencaValidaAte: string | null;
}

export async function obterFaturamento(token: string): Promise<ResumoDeFaturamento | null> {
  const resposta = await fetch(`${URL_BASE}/api/faturamento`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (resposta.status === 404) return null; // instalação ainda sem lote de faturamento sincronizado — não é erro.
  if (!resposta.ok) {
    const corpo = await resposta.json().catch(() => null);
    throw new ErroDaApi(corpo?.erro ?? "Não foi possível carregar o faturamento.", resposta.status);
  }

  return resposta.json();
}
