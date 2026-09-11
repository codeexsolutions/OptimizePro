namespace OptimizePro.Sincronizacao;

/// <summary>
/// Um ID aleatório, estável, gerado uma vez por máquina física (§24.1) — não é segredo (ao
/// contrário da chave de API, não precisa de DPAPI), só precisa sobreviver a reinstalações do
/// app pra a Central saber que é "a mesma máquina de sempre" tentando provisionar de novo. Sem
/// isto, várias máquinas da mesma gráfica (mesma licença, mesmo ClienteIdHash) pareceriam uma
/// máquina só pra Central, e só a primeira conseguiria sincronizar.
/// </summary>
public static class IdentidadeDaMaquina
{
    public static string ObterOuCriar(string arquivo)
    {
        if (File.Exists(arquivo))
        {
            var existente = File.ReadAllText(arquivo).Trim();
            if (Guid.TryParse(existente, out _)) return existente;
        }

        var novo = Guid.NewGuid().ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
        File.WriteAllText(arquivo, novo);
        return novo;
    }
}
