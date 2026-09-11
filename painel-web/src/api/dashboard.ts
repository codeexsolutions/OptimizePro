import { ErroDaApi } from "./central";

const URL_BASE = import.meta.env.VITE_CENTRAL_URL as string;

export interface Maquina {
  id: string;
  nome: string;
  tipo: string;
  habilitada: boolean;
  host: string | null;
  ip: string | null;
}

export interface ImpressoraResumo {
  maquinaId: string;
  nome: string;
  tipo: string;
  habilitada: boolean;
  trabalhosHoje: number;
  metragemHoje: number;
  ultimoTrabalho: string | null;
  ultimoHorario: string | null;
}

export interface RegistroDeImpressao {
  id: string;
  maquinaId: string;
  nomeDaMaquina: string | null;
  dataHora: string;
  data: string;
  tarefa: string | null;
  areaDeImpressao: number;
  comprimentoDeImpressao: number;
  status: string | null;
  cancelada: boolean;
  comErro: boolean;
  tintaMl: number;
}

export interface SemanaDeReposicao {
  inicioDaSemana: string;
  fimDaSemana: string;
  metragemTotal: number;
  quantidade: number;
  itens: RegistroDeImpressao[];
}

export interface RespostaDeReposicao {
  semanas: SemanaDeReposicao[];
  metragemTotal: number;
  quantidadeTotal: number;
}

export interface PedidoItem {
  id: string;
  posicao: number;
  registroId: string;
  nomeDoCliente: string | null;
  tecido: string | null;
  tarefa: string | null;
  nomeDaMaquina: string | null;
  comprimentoDeImpressao: number | null;
  data: string | null;
  statusNaCalandra: string;
}

export interface Pedido {
  id: string;
  criadoEm: string;
  status: string;
  observacao: string | null;
  itens: PedidoItem[];
}

export interface OrdemDeServico {
  id: string;
  nomeDoCliente: string;
  tecido: string | null;
  tamanhoDeImpressao: string | null;
  metros: number | null;
  operador: string | null;
  maquina: string | null;
  data: string;
  observacao: string | null;
  quantidadeDeImagens: number;
}

async function buscar<T>(caminho: string, token: string): Promise<T> {
  const resposta = await fetch(`${URL_BASE}${caminho}`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!resposta.ok) {
    const corpo = await resposta.json().catch(() => null);
    throw new ErroDaApi(corpo?.erro ?? "Não foi possível carregar os dados.", resposta.status);
  }

  return resposta.json();
}

export const obterMaquinas = (token: string) => buscar<Maquina[]>("/api/dashboard/maquinas", token);
export const obterImpressoras = (token: string) => buscar<ImpressoraResumo[]>("/api/dashboard/impressoras", token);
export const obterHistorico = (token: string) => buscar<RegistroDeImpressao[]>("/api/dashboard/historico", token);
export const obterReposicao = (token: string) => buscar<RespostaDeReposicao>("/api/dashboard/reposicao", token);
export const obterPedidos = (token: string) => buscar<Pedido[]>("/api/dashboard/pedidos", token);
export const obterOrdensDeServico = (token: string) => buscar<OrdemDeServico[]>("/api/dashboard/ordens-servico", token);
