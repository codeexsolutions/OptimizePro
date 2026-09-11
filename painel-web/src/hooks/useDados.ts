import { useEffect, useState } from "react";
import { useAutenticacao } from "../auth/AuthContext";
import { ErroDaApi } from "../api/central";

// O mesmo padrão "carregando/erro/dados" repetido nas 6 telas de dashboard — só isso, sem
// cache/revalidação: o painel remoto lê um espelho já sincronizado, não precisa ficar
// refazendo a mesma chamada toda hora.
export function useDados<T>(buscar: (token: string) => Promise<T>, dependencias: unknown[] = []) {
  const { sessao } = useAutenticacao();
  const [dados, setDados] = useState<T | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!sessao) return;
    let cancelado = false;

    setCarregando(true);
    setErro(null);
    buscar(sessao.token)
      .then((resultado) => {
        if (!cancelado) setDados(resultado);
      })
      .catch((e) => {
        if (cancelado) return;
        setErro(e instanceof ErroDaApi ? e.message : "Não foi possível carregar os dados.");
      })
      .finally(() => {
        if (!cancelado) setCarregando(false);
      });

    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessao?.token, ...dependencias]);

  return { dados, carregando, erro };
}
