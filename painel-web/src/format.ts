// Porte bem reduzido de impressoras/formato.ts (optmize-full) / FormatoImpressoras (app desktop)
// — só o que as telas de dashboard do painel remoto precisam.

export function metros(valor: number | null | undefined): string {
  return `${Number(valor || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} m`;
}

export function reais(valor: number | null | undefined): string {
  return Number(valor || 0).toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export function dataBr(iso: string | null | undefined): string {
  if (!iso) return "";
  const [ano, mes, dia] = iso.slice(0, 10).split("-");
  return `${dia}/${mes}/${ano}`;
}

export function dataHoraBr(iso: string | null | undefined): string {
  if (!iso) return "";
  const partes = iso.split(" ");
  return partes.length > 1 ? `${dataBr(partes[0])} ${partes[1].slice(0, 5)}` : dataBr(iso);
}
