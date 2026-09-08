namespace OptimizePro.Services.Armazenamento;

/// <summary>
/// Substitui <c>paths.js</c> (§7.1 da especificação) — pasta de dados gravável do usuário,
/// resolvida via <see cref="Environment.SpecialFolder.LocalApplicationData"/> (equivalente ao
/// <c>%APPDATA%\com.arteof.optimize</c> do Tauri atual — §15 da arquitetura).
/// </summary>
public sealed class CaminhosDoApp
{
    public string PastaDeDados { get; }
    public string BancoDeDados => Path.Combine(PastaDeDados, "dados.db");
    public string PastaUploads => Path.Combine(PastaDeDados, "uploads");
    public string PastaUploadsArtesMolde => Path.Combine(PastaUploads, "artes-molde");
    public string PastaUploadsProjetos => Path.Combine(PastaUploads, "projetos");
    public string ArquivoResultadoDisparo => Path.Combine(PastaDeDados, "resultado-envio.csv");
    public string PastaSessaoWhatsApp => Path.Combine(PastaDeDados, "whatsapp-sessao");
    public string ArquivoDeLicenca => Path.Combine(PastaDeDados, "licenca.dat");

    public CaminhosDoApp() : this(PastaPadrao()) { }

    public CaminhosDoApp(string pastaDeDados)
    {
        PastaDeDados = pastaDeDados;
        Directory.CreateDirectory(PastaUploadsArtesMolde);
        Directory.CreateDirectory(PastaUploadsProjetos);
    }

    private static string PastaPadrao()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "Optimize");
    }
}
