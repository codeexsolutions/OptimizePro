using System.Security.Cryptography;
using OptimizePro.Licenciamento;

// ============================================================================
// Ferramenta de geração de licença — NUNCA vai pro cliente. Só quem roda isto
// tem a chave PRIVADA (o arquivo .pem que "gerar-chave" cria); o app do
// cliente só carrega a chave PÚBLICA (embutida em OptimizePro.Services), que
// não serve pra forjar um código novo, só pra conferir um já existente.
//
// Uso:
//   dotnet run -- gerar-chave [--saida chave-privada.pem]
//   dotnet run -- gerar-codigo --chave chave-privada.pem --cliente "nome/email" --dias 30 [--tipo pago|teste]
// ============================================================================

if (args.Length == 0)
{
    MostrarAjuda();
    return 1;
}

switch (args[0])
{
    case "gerar-chave":
        GerarChave(LerOpcao(args, "--saida") ?? "chave-privada.pem");
        return 0;

    case "gerar-codigo":
        return GerarCodigo(args);

    default:
        MostrarAjuda();
        return 1;
}

static void MostrarAjuda()
{
    Console.WriteLine("""
        Ferramenta de geração de licença do OptimizePro — NUNCA distribuir junto com o app do cliente.

        Uso:
          dotnet run -- gerar-chave [--saida chave-privada.pem]
              Cria um par de chaves ECDSA P-256 novo. Salva a chave PRIVADA no arquivo indicado
              (guarde em local seguro, NUNCA no mesmo repositório do app) e imprime a chave
              PÚBLICA em Base64 — cole essa saída em LicencaService.ChavePublicaBase64.

          dotnet run -- gerar-codigo --chave chave-privada.pem --cliente "nome ou e-mail" --dias 30 [--tipo pago|teste]
              Gera um código de licença válido por N dias a partir de hoje. --tipo é só
              informativo (aparece na tela do cliente); "teste" pra período de avaliação,
              "pago" (padrão) pra licença mensal de verdade.
        """);
}

static void GerarChave(string caminhoDeSaida)
{
    if (File.Exists(caminhoDeSaida))
    {
        Console.Error.WriteLine($"Já existe um arquivo em '{caminhoDeSaida}' — apague ou escolha outro --saida antes de gerar uma chave nova (gerar outra chave invalida TODO código já emitido com a antiga).");
        Environment.Exit(1);
    }

    using var chave = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    File.WriteAllText(caminhoDeSaida, chave.ExportECPrivateKeyPem());
    var publicaBase64 = Convert.ToBase64String(chave.ExportSubjectPublicKeyInfo());

    Console.WriteLine($"Chave privada salva em: {Path.GetFullPath(caminhoDeSaida)}");
    Console.WriteLine();
    Console.WriteLine("GUARDE ESSE ARQUIVO EM SEGURANÇA — quem tiver ele consegue gerar código de licença válido pra sempre.");
    Console.WriteLine("Nunca commite no mesmo repositório do app, nunca mande pro cliente.");
    Console.WriteLine();
    Console.WriteLine("Chave PÚBLICA (cole em LicencaService.ChavePublicaBase64, no código do app):");
    Console.WriteLine(publicaBase64);
}

static int GerarCodigo(string[] args)
{
    var caminhoDaChave = LerOpcao(args, "--chave");
    var cliente = LerOpcao(args, "--cliente");
    var diasTexto = LerOpcao(args, "--dias");
    var tipoTexto = LerOpcao(args, "--tipo") ?? "pago";

    if (caminhoDaChave is null || cliente is null || diasTexto is null)
    {
        Console.Error.WriteLine("Uso: gerar-codigo --chave chave-privada.pem --cliente \"nome/e-mail\" --dias 30 [--tipo pago|teste]");
        return 1;
    }

    if (!File.Exists(caminhoDaChave))
    {
        Console.Error.WriteLine($"Arquivo de chave não encontrado: {caminhoDaChave}");
        return 1;
    }

    if (!int.TryParse(diasTexto, out var dias) || dias <= 0)
    {
        Console.Error.WriteLine($"--dias precisa ser um número inteiro positivo (recebido: '{diasTexto}').");
        return 1;
    }

    var tipo = tipoTexto.Equals("teste", StringComparison.OrdinalIgnoreCase) ? TipoDeLicenca.Teste : TipoDeLicenca.Paga;

    using var chave = ECDsa.Create();
    chave.ImportFromPem(File.ReadAllText(caminhoDaChave));

    var validoAte = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(dias);
    var clienteIdHash = CodificadorDeLicenca.HashDoCliente(cliente);
    var codigo = CodificadorDeLicenca.Gerar(chave, validoAte, clienteIdHash, tipo);

    Console.WriteLine($"Cliente:    {cliente}");
    Console.WriteLine($"Tipo:       {(tipo == TipoDeLicenca.Teste ? "Teste" : "Pago")}");
    Console.WriteLine($"Válido até: {validoAte:dd/MM/yyyy} ({dias} dia(s) a partir de hoje)");
    Console.WriteLine();
    Console.WriteLine("Código de licença:");
    Console.WriteLine(codigo);

    return 0;
}

static string? LerOpcao(string[] args, string nome)
{
    var indice = Array.IndexOf(args, nome);
    return indice >= 0 && indice + 1 < args.Length ? args[indice + 1] : null;
}
